namespace PhotoOrganizer.Application.Crawler;

public sealed class CrawlerStatusDto
{
    public string Status { get; set; } = "idle";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Mode { get; set; }
    public string? TargetStep { get; set; }
    public int FilesScanned { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesErrored { get; set; }
    public string? ErrorMessage { get; set; }
}
