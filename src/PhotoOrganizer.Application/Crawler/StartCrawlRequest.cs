namespace PhotoOrganizer.Application.Crawler;

public sealed class StartCrawlRequest
{
    public string Mode { get; set; } = "incremental";
    public string? Step { get; set; }
}
