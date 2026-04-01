using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Pipeline;

public sealed class ProcessingContext
{
    public required string FilePath { get; init; }
    public required PhotoMetaSidecar Sidecar { get; init; }
    public required CrawledFileRecord DbRecord { get; init; }
}
