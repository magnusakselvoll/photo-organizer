namespace PhotoOrganizer.Application.Crawler;

public sealed class CrawlerSettings
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string? ConfigPath { get; set; }
    public string DatabasePath { get; set; } = "./crawler.db";
}
