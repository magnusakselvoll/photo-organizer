namespace PhotoOrganizer.Crawler.Pipeline;

public interface IBatchProcessingStep
{
    string Name { get; }
    int Version { get; }
    IReadOnlyList<string> DependsOn { get; }
    Task ExecuteAsync(BatchProcessingContext context);
}
