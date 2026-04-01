namespace PhotoOrganizer.Crawler.Data;

public interface ICrawlLogRepository
{
    Task<int> StartCrawlAsync(string mode, string? targetStep = null);
    Task CompleteCrawlAsync(int crawlId, string status, int filesScanned, int filesProcessed, int filesErrored, string? errorMessage = null);
}
