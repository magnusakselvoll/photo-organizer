using PhotoOrganizer.Domain;

namespace PhotoOrganizer.Crawler.Discovery;

public sealed class FileDiscoverer : IFileDiscoverer
{
    public IReadOnlyList<DiscoveredFile> Discover(string folderPath)
    {
        var results = new List<DiscoveredFile>();
        foreach (var filePath in ResilientFileWalker.EnumerateFiles(folderPath, "*"))
        {
            if (!SupportedPhotoExtensions.IsSupported(filePath))
                continue;

            var info = new FileInfo(filePath);
            results.Add(new DiscoveredFile(filePath, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
        }
        return results;
    }
}
