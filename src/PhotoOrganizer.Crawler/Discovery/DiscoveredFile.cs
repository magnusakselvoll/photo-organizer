namespace PhotoOrganizer.Crawler.Discovery;

public sealed record DiscoveredFile(string FilePath, DateTimeOffset LastModified);
