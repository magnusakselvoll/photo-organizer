using System.Text.Json;
using PhotoOrganizer.Crawler.Configuration;

namespace PhotoOrganizer.Crawler.Commands;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static async Task<CrawlerConfig> LoadAsync(string? configPath)
    {
        var path = configPath ?? "crawler-config.json";
        if (!File.Exists(path))
            return new CrawlerConfig();

        await using var stream = File.OpenRead(path);
        var wrapper = await JsonSerializer.DeserializeAsync<ConfigWrapper>(stream, Options);
        return wrapper?.Crawler ?? new CrawlerConfig();
    }

    private sealed class ConfigWrapper
    {
        public CrawlerConfig? Crawler { get; set; }
    }
}
