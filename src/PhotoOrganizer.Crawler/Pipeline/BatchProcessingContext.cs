using PhotoOrganizer.Crawler.Sidecars;

namespace PhotoOrganizer.Crawler.Pipeline;

public sealed class BatchProcessingContext
{
    public required IReadOnlyList<string> FilePaths { get; init; }
    public required ISidecarStore SidecarStore { get; init; }

    /// <summary>
    /// Returns the last-modified time for a file path.
    /// Defaults to <see cref="File.GetLastWriteTimeUtc"/> when not set.
    /// Override in tests to avoid real file system access.
    /// </summary>
    public Func<string, DateTime>? GetLastModified { get; init; }
}
