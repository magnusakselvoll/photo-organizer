using PhotoOrganizer.Crawler.Sidecars;
using PhotoOrganizer.Domain.Models;
using Serilog;

namespace PhotoOrganizer.Crawler.Discovery;

public sealed class CrawlTargetResolver : ICrawlTargetResolver
{
    private readonly ISidecarStore _sidecarStore;
    private readonly IFileDiscoverer _discoverer;

    public CrawlTargetResolver(ISidecarStore sidecarStore, IFileDiscoverer discoverer)
    {
        _sidecarStore = sidecarStore;
        _discoverer = discoverer;
    }

    public async Task<IReadOnlyList<CrawlTarget>> ResolveAsync(IReadOnlyList<string> scanRoots)
    {
        var results = new List<CrawlTarget>();

        foreach (var scanRoot in scanRoots)
        {
            if (!Directory.Exists(scanRoot))
            {
                Log.Warning("ScanRoot does not exist, skipping: {ScanRoot}", scanRoot);
                continue;
            }

            // Discover all _folder.json files beneath the scan root (mirrors FileSystemFolderRepository)
            var unitMap = new Dictionary<string, FolderSidecar>(StringComparer.OrdinalIgnoreCase);
            foreach (var sidecarFile in ResilientFileWalker.EnumerateFiles(scanRoot, "_folder.json"))
            {
                var dir = Path.GetDirectoryName(sidecarFile);
                if (dir is null)
                    continue;

                var sidecar = await _sidecarStore.ReadFolderSidecarAsync(dir);
                if (sidecar is null)
                    continue;

                unitMap[dir] = sidecar;
            }

            if (unitMap.Count == 0)
            {
                Log.Warning("No _folder.json files found under {ScanRoot}, skipping", scanRoot);
                continue;
            }

            var unitDirs = new HashSet<string>(unitMap.Keys, StringComparer.OrdinalIgnoreCase);

            // Discover all photos once and bucket each to its nearest ancestor unit
            var discovered = _discoverer.Discover(scanRoot);
            var buckets = new Dictionary<string, List<DiscoveredFile>>(StringComparer.OrdinalIgnoreCase);

            foreach (var unitDir in unitDirs)
                buckets[unitDir] = [];

            foreach (var file in discovered)
            {
                var owner = FindNearestAncestorUnit(file.FilePath, unitDirs);
                if (owner is null)
                    continue; // not beneath any unit folder — skipped silently

                buckets[owner].Add(file);
            }

            // Emit one CrawlTarget per unit, in a deterministic order (path-sorted)
            foreach (var (dir, sidecar) in unitMap.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                results.Add(new CrawlTarget(dir, sidecar, buckets[dir]));
        }

        return results;
    }

    /// <summary>
    /// Walks up the directory hierarchy from <paramref name="filePath"/> until a directory
    /// that belongs to a crawl unit is found. Returns <c>null</c> if none is found.
    /// </summary>
    private static string? FindNearestAncestorUnit(string filePath, HashSet<string> unitDirs)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir is not null)
        {
            if (unitDirs.Contains(dir))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
