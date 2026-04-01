namespace PhotoOrganizer.Crawler.Configuration;

public sealed class CrawlerConfig
{
    public string DatabasePath { get; set; } = "./crawler.db";
    public int ScheduleIntervalMinutes { get; set; } = 60;
    public bool OrphanedSidecarCleanup { get; set; } = false;
    public List<string> ScanRoots { get; set; } = [];
}
