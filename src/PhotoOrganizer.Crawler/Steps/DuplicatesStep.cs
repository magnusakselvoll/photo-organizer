using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PhotoOrganizer.Crawler.Pipeline;
using PhotoOrganizer.Domain.Models;
using Serilog;

namespace PhotoOrganizer.Crawler.Steps;

public sealed partial class DuplicatesStep : IBatchProcessingStep
{
    // Leading date or timestamp prefix, e.g.:
    //   20260405_  20260405-  20260405  (YYYYMMDD / DDMMYYYY / any 8-digit date)
    //   20260405_123456_  20260405123456  (with time component)
    // Matches 8 digits optionally followed by sep+6 digits (time), or 14 digits,
    // then an optional trailing separator before the real name.
    [GeneratedRegex(@"^(\d{8}[_\-]\d{6}|\d{14}|\d{8})[_\-]?", RegexOptions.None)]
    private static partial Regex DatePrefixPattern();

    // macOS "copy" suffix: " copy", " copy 2", " copy 37", etc.
    [GeneratedRegex(@"\s+copy(\s+\d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex CopySuffixPattern();

    // Edit suffixes to strip, ordered longest-first to avoid partial matches.
    private static readonly string[] EditSuffixes =
    [
        "_retouched",
        "_edited",
        "-edited",
        "_edit",
        "-edit",
        "_hdr",
        "-hdr",
    ];

    public string Name => "duplicates";
    public int Version => 1;
    public IReadOnlyList<string> DependsOn => ["metadata"];

    public async Task ExecuteAsync(BatchProcessingContext context)
    {
        // Group files by normalized name
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var filePath in context.FilePaths)
        {
            var normalized = NormalizeName(filePath);
            if (!groups.TryGetValue(normalized, out var list))
            {
                list = [];
                groups[normalized] = list;
            }
            list.Add(filePath);
        }

        // Cache folder sidecars by directory to avoid re-reading
        var folderSidecarCache = new Dictionary<string, FolderSidecar?>(StringComparer.OrdinalIgnoreCase);

        // Process each group
        foreach (var (normalizedName, filePaths) in groups)
        {
            if (filePaths.Count < 2)
            {
                // Single file — clear any leftover duplicate info
                var sidecar = await context.SidecarStore.ReadPhotoMetaAsync(filePaths[0])
                    ?? new PhotoMetaSidecar();
                if (sidecar.DuplicateGroupId is not null || sidecar.IsPreferred)
                {
                    sidecar.DuplicateGroupId = null;
                    sidecar.IsPreferred = false;
                    sidecar.CrawlSteps[Name] = new CrawlStepRecord { Version = Version, CompletedAt = DateTimeOffset.UtcNow };
                    await context.SidecarStore.WritePhotoMetaAsync(filePaths[0], sidecar);
                }
                continue;
            }

            var groupId = DeterministicGuid(normalizedName);
            Log.Debug("Duplicate group {GroupId} for normalized name '{NormalizedName}' ({Count} files)",
                groupId, normalizedName, filePaths.Count);

            // Load sidecars and folder info for all files in the group
            var getLastModified = context.GetLastModified ?? (path => File.GetLastWriteTimeUtc(path));
            var entries = new List<(string FilePath, PhotoMetaSidecar Sidecar, string FolderType, DateTime LastModified)>();
            foreach (var filePath in filePaths)
            {
                var sidecar = await context.SidecarStore.ReadPhotoMetaAsync(filePath)
                    ?? new PhotoMetaSidecar();

                var dir = Path.GetDirectoryName(filePath) ?? filePath;
                if (!folderSidecarCache.TryGetValue(dir, out var folderSidecar))
                {
                    folderSidecar = await context.SidecarStore.ReadFolderSidecarAsync(dir);
                    folderSidecarCache[dir] = folderSidecar;
                }
                var folderType = folderSidecar?.Type ?? "mixed";
                var lastModified = getLastModified(filePath);
                entries.Add((filePath, sidecar, folderType, lastModified));
            }

            // Determine preferred: edits > originals > mixed, then most recently modified, then alphabetical
            var preferred = entries
                .OrderBy(e => FolderTypePriority(e.FolderType))
                .ThenByDescending(e => e.LastModified)
                .ThenBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase)
                .First();

            // Write updated sidecars
            var completedAt = DateTimeOffset.UtcNow;
            foreach (var (filePath, sidecar, _, _) in entries)
            {
                sidecar.DuplicateGroupId = groupId;
                sidecar.IsPreferred = filePath == preferred.FilePath;
                sidecar.CrawlSteps[Name] = new CrawlStepRecord { Version = Version, CompletedAt = completedAt };
                await context.SidecarStore.WritePhotoMetaAsync(filePath, sidecar);
            }
        }
    }

    public static string NormalizeName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);

        // 1. Strip leading date/timestamp prefix
        var dateMatch = DatePrefixPattern().Match(name);
        if (dateMatch.Success && dateMatch.Length < name.Length)
            name = name[dateMatch.Length..];

        // 2. Strip trailing macOS copy suffix (" copy", " copy 2", ...)
        name = CopySuffixPattern().Replace(name, string.Empty);

        // 3. Strip at most one trailing edit suffix
        foreach (var suffix in EditSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name.ToLowerInvariant();
    }

    private static int FolderTypePriority(string folderType) => folderType switch
    {
        "edits" => 0,
        "originals" => 1,
        _ => 2  // mixed or unknown
    };

    private static Guid DeterministicGuid(string normalizedName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName));
        return new Guid(hash[..16]);
    }
}
