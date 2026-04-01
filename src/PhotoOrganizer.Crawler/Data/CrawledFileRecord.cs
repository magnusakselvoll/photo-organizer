namespace PhotoOrganizer.Crawler.Data;

public sealed class CrawledFileRecord
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? FileHash { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset? LastCrawledAt { get; set; }
    public bool Deleted { get; set; }
}
