namespace PhotoOrganizer.Crawler.Discovery;

public interface IFileDiscoverer
{
    IReadOnlyList<DiscoveredFile> Discover(string folderPath);
}
