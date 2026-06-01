using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Discovery;

/// <summary>
/// Represents a single crawl unit: a folder that contains a <c>_folder.json</c>,
/// with its sidecar and the photo files that belong to it.
/// </summary>
public sealed record CrawlTarget(string FolderPath, FolderSidecar Sidecar, IReadOnlyList<DiscoveredFile> Files);

/// <summary>
/// Resolves a list of scan roots into independent crawl units by recursively
/// discovering all <c>_folder.json</c> files beneath each root.
/// </summary>
public interface ICrawlTargetResolver
{
    Task<IReadOnlyList<CrawlTarget>> ResolveAsync(IReadOnlyList<string> scanRoots);
}
