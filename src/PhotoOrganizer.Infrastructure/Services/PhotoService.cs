using System.Text;
using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Services;

public sealed class PhotoService(IPhotoRepository repository) : IPhotoService
{
    // ─── Sorted-view cache (see ADR 010) ─────────────────────────────────────
    //
    // The singleton PhotoService caches a point-in-time sorted snapshot of all
    // displayable photos. The cache is keyed by IPhotoRepository.Version, which
    // is bumped on every AddPhoto call by the background indexer. Once indexing
    // is complete the version stabilises and every browse request is a cache hit,
    // eliminating the per-request O(N log N) snapshot+sort that caused multi-
    // second stalls on 10k–100k photo libraries.
    //
    // Thread-safety: Volatile.Read/Write ensures the reference is exchanged
    // atomically without a lock. Concurrent rebuilds during the indexing window
    // are benign — last writer wins and all candidates produce equivalent results
    // for the same version.

    private sealed record CachedView(long Version, IReadOnlyList<Photo> Sorted);
    private CachedView? _cache;

    private async Task<IReadOnlyList<Photo>> GetSortedDisplayableAsync()
    {
        // Read Version BEFORE snapshotting: conservative — we may rebuild
        // unnecessarily if the version increments between these two reads, but
        // we never label a snapshot with a version newer than it actually reflects.
        var version = repository.Version;
        var cached = Volatile.Read(ref _cache);

        if (cached is not null && cached.Version == version)
            return cached.Sorted;

        var all = await repository.GetAllPhotosAsync();

        var sorted = all
            .Where(p => DisplayableImageFormats.IsDisplayable(p.FilePath))
            .OrderByDescending(p => p.CapturedAt ?? p.FileModifiedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(p => p.Id)
            .ToList();

        Volatile.Write(ref _cache, new CachedView(version, sorted));
        return sorted;
    }

    // ─── Public service methods ───────────────────────────────────────────────

    public async Task<PhotoPageDto> GetPhotosAsync(PhotoFilter filter)
    {
        var sorted = await GetSortedDisplayableAsync();
        var filtered = ApplyNarrowing(sorted, filter);
        var totalCount = filtered.Count;

        // Keyset (cursor) path — used by the infinite-scroll grid.
        if (filter.Limit is { } limit)
        {
            var (afterTicks, afterId) = filter.Cursor is not null
                ? DecodeCursor(filter.Cursor)
                : (long.MaxValue, Guid.Empty);

            var page = filtered
                .SkipWhile(p =>
                {
                    var ticks = EffectiveTicks(p);
                    return ticks > afterTicks || (ticks == afterTicks && p.Id.CompareTo(afterId) >= 0);
                })
                .Take(limit)
                .ToList();

            var nextCursor = page.Count == limit
                ? EncodeCursor(page[^1])
                : null;

            return new PhotoPageDto
            {
                Items = page.Select(p => ToDto(p)).ToList(),
                TotalCount = totalCount,
                Page = 1,
                PageSize = limit,
                NextCursor = nextCursor,
            };
        }

        // Legacy offset path — kept intact for slideshow and other callers.
        var items = filtered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => ToDto(p))
            .ToList();

        return new PhotoPageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize,
        };
    }

    public async Task<PhotoDto?> GetPhotoByIdAsync(Guid id)
    {
        var photo = await repository.GetByIdAsync(id);
        if (photo is null)
            return null;

        // Populate sibling versions when the photo belongs to a duplicate group.
        IReadOnlyList<PhotoVersionDto> versions = [];
        if (photo.DuplicateGroupId is not null)
        {
            var all = await repository.GetAllPhotosAsync();
            versions = all
                .Where(p => p.DuplicateGroupId == photo.DuplicateGroupId)
                .OrderByDescending(p => p.IsPreferred)
                .ThenBy(p => p.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(p => new PhotoVersionDto
                {
                    Id = p.Id,
                    FileName = Path.GetFileName(p.FilePath),
                    FolderType = p.FolderType.ToString(),
                    FilePath = p.FilePath,
                    IsPreferred = p.IsPreferred,
                })
                .ToList();
        }

        return ToDto(photo, versions);
    }

    // ─── Per-request narrowing filters ───────────────────────────────────────
    //
    // Applied to the pre-sorted cached list. All filters are order-preserving
    // (Where predicates / Deduplicate), so the result inherits the sort order
    // established in GetSortedDisplayableAsync.

    private static List<Photo> ApplyNarrowing(IReadOnlyList<Photo> sorted, PhotoFilter filter)
    {
        IEnumerable<Photo> result = sorted;

        if (filter.Folder is not null)
            result = result.Where(p => p.FilePath.StartsWith(filter.Folder, StringComparison.OrdinalIgnoreCase));

        if (filter.Type is not null && !filter.Type.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var folderType = FolderTypeExtensions.Parse(filter.Type);
            result = result.Where(p => p.FolderType == folderType);
        }

        if (filter.FileName is { Length: > 0 } fn)
            result = result.Where(p => Path.GetFileName(p.FilePath).Contains(fn, StringComparison.OrdinalIgnoreCase));

        // Date-range filtering on effective date (CapturedAt ?? FileModifiedAt).
        // Dates are compared in UTC for consistency with the keyset cursor (EffectiveTicks uses UtcTicks).
        // Photos with a null effective date are excluded whenever any bound is set.
        if (filter.DateFrom is { } dateFrom)
        {
            var fromUtc = new DateTimeOffset(dateFrom.Year, dateFrom.Month, dateFrom.Day, 0, 0, 0, TimeSpan.Zero);
            result = result.Where(p => (p.CapturedAt ?? p.FileModifiedAt) >= fromUtc);
        }

        if (filter.DateTo is { } dateTo)
        {
            // Exclusive upper bound at the start of the next day so the whole to-day is included.
            var toExclusiveUtc = new DateTimeOffset(dateTo.Year, dateTo.Month, dateTo.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1);
            result = result.Where(p => (p.CapturedAt ?? p.FileModifiedAt) < toExclusiveUtc);
        }

        if (filter.Deduplicated)
            result = Deduplicate(result);

        return result.ToList();
    }

    private static IEnumerable<Photo> Deduplicate(IEnumerable<Photo> photos)
    {
        var seen = new HashSet<Guid>();
        foreach (var photo in photos)
        {
            if (photo.DuplicateGroupId is null)
            {
                yield return photo;
            }
            else if (photo.IsPreferred && seen.Add(photo.DuplicateGroupId.Value))
            {
                yield return photo;
            }
        }
    }

    private static PhotoDto ToDto(Photo photo, IReadOnlyList<PhotoVersionDto>? versions = null) => new()
    {
        Id = photo.Id,
        FilePath = photo.FilePath,
        FileName = Path.GetFileName(photo.FilePath),
        CapturedAt = photo.CapturedAt,
        EffectiveDate = photo.CapturedAt ?? photo.FileModifiedAt,
        FolderType = photo.FolderType.ToString(),
        DuplicateGroupId = photo.DuplicateGroupId,
        IsPreferred = photo.IsPreferred,
        Tags = photo.Tags,
        Versions = versions ?? [],
    };

    // ─── Cursor helpers ───────────────────────────────────────────────────────

    /// <summary>Returns the UTC ticks of the sort key for a photo: CapturedAt ?? FileModifiedAt ?? MinValue.</summary>
    public static long EffectiveTicks(Photo p) =>
        (p.CapturedAt ?? p.FileModifiedAt ?? DateTimeOffset.MinValue).UtcTicks;

    /// <summary>
    /// Encodes the position of <paramref name="photo"/> into an opaque base64url cursor string.
    /// Format: "{effectiveTicks}_{id:N}" — readable in logs, collision-free.
    /// </summary>
    public static string EncodeCursor(Photo photo)
    {
        var raw = $"{EffectiveTicks(photo)}_{photo.Id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Decodes an opaque cursor back to <c>(effectiveTicks, id)</c>.
    /// Returns <c>(long.MaxValue, Guid.Empty)</c> if the cursor is malformed.
    /// </summary>
    public static (long Ticks, Guid Id) DecodeCursor(string cursor)
    {
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            var mod = padded.Length % 4;
            if (mod != 0) padded += new string('=', 4 - mod);
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var sep = raw.IndexOf('_');
            if (sep < 0) return (long.MaxValue, Guid.Empty);
            var ticks = long.Parse(raw[..sep]);
            var id = Guid.ParseExact(raw[(sep + 1)..], "N");
            return (ticks, id);
        }
        catch
        {
            return (long.MaxValue, Guid.Empty);
        }
    }
}
