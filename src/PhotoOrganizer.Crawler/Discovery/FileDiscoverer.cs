namespace PhotoOrganizer.Crawler.Discovery;

public sealed class FileDiscoverer : IFileDiscoverer
{
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic",
        ".cr2", ".cr3", ".orf", ".arw", ".nef", ".rw2",
        ".tiff", ".tif"
    };

    public IReadOnlyList<DiscoveredFile> Discover(string folderPath)
    {
        var results = new List<DiscoveredFile>();
        foreach (var filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(filePath);
            if (!PhotoExtensions.Contains(ext))
                continue;

            var info = new FileInfo(filePath);
            results.Add(new DiscoveredFile(filePath, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
        }
        return results;
    }
}
