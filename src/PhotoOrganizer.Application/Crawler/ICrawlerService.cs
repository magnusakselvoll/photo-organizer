namespace PhotoOrganizer.Application.Crawler;

public interface ICrawlerService
{
    Task<CrawlerStatusDto> GetStatusAsync();
    /// <returns>true if the crawl was started, false if already running.</returns>
    Task<bool> StartCrawlAsync(StartCrawlRequest request);
}
