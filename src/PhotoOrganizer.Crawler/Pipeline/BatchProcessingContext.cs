using PhotoOrganizer.Crawler.Sidecars;

namespace PhotoOrganizer.Crawler.Pipeline;

public sealed class BatchProcessingContext
{
    public required IReadOnlyList<string> FilePaths { get; init; }
    public required ISidecarStore SidecarStore { get; init; }
}
