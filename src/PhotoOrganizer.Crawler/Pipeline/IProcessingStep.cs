namespace PhotoOrganizer.Crawler.Pipeline;

public interface IProcessingStep
{
    string Name { get; }
    int Version { get; }
    IReadOnlyList<string> DependsOn { get; }
    Task ExecuteAsync(ProcessingContext context);
}
