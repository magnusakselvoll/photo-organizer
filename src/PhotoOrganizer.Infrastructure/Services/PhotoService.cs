using System.Text;
using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Services;

public sealed class PhotoService(IPhotoRepository repository) : IPhotoService
{
    public async Task<PhotoPageDto> GetPhotosAsync(PhotoFilter filter)
    {
        var all = await repository.GetAllPhotosAsync();
        var filtered = ApplyFilters(all, filter);
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
                    FileName = p.FileName,
                    FolderType = p.FolderType.ToString(),
                    FilePath = p.FilePath,
                    IsPreferred = p.IsPreferred,
                })
                .ToList();
        }

        return ToDto(photo, versions);
    }

    private static List<Photo> ApplyFilters(IReadOnlyList<Photo> photos, PhotoFilter filter)
    {
        IEnumerable<Photo> result = photos;

        // Only serve photos the browser can actually display (natively or via transcoding).
        // Non-displayable files (RAW formats, bare TIFF) are never shown in the grid or
        // slideshow but remain accessible via the version panel's /image download endpoint.
        result = result.Where(p => DisplayableImageFormats.IsDisplayable(p.FilePath));

        if (filter.Folder is not null)
            result = result.Where(p => p.FilePath.StartsWith(filter.Folder, StringComparison.OrdinalIgnoreCase));

        if (filter.Type is not null && !filter.Type.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var folderType = FolderTypeExtensions.Parse(filter.Type);
            result = result.Where(p => p.FolderType == folderType);
        }

        if (filter.Deduplicated)
            result = Deduplicate(result);

        // Display order: capturedAt descending, falling back to file-system mtime when absent.
        // Photos still being indexed (FileModifiedAt also null) sort to the end.
        // Id is a stable tiebreaker so keyset cursors produce deterministic pages.
        result = result
            .OrderByDescending(p => p.CapturedAt ?? p.FileModifiedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(p => p.Id);

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
        FileName = photo.FileName,
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
