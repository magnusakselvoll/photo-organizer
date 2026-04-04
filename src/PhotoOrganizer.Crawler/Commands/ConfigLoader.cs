using System.Text.Json;
using System.Text.Json.Serialization;
using PhotoOrganizer.Crawler.Configuration;

namespace PhotoOrganizer.Crawler.Commands;

public static class ConfigLoader
{
    public static string ResolvePath(string? configPath) => configPath ?? "crawler-config.json";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<CrawlerConfig> LoadAsync(string? configPath)
    {
        var path = ResolvePath(configPath);
        if (!File.Exists(path))
            return new CrawlerConfig();

        await using var stream = File.OpenRead(path);
        var wrapper = await JsonSerializer.DeserializeAsync<ConfigWrapper>(stream, ReadOptions);
        return wrapper?.Crawler ?? new CrawlerConfig();
    }

    public static async Task SaveAsync(string? configPath, CrawlerConfig config)
    {
        var path = ResolvePath(configPath);
        var wrapper = new ConfigWrapper { Crawler = config };
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, wrapper, WriteOptions);
        await stream.FlushAsync();
    }

    private sealed class ConfigWrapper
    {
        public CrawlerConfig? Crawler { get; set; }
    }
}
