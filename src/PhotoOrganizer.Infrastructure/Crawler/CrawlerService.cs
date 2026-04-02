using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PhotoOrganizer.Application.Crawler;

namespace PhotoOrganizer.Infrastructure.Crawler;

public sealed class CrawlerService(IOptions<CrawlerSettings> options) : ICrawlerService
{
    private readonly CrawlerSettings _settings = options.Value;

    public async Task<CrawlerStatusDto> GetStatusAsync()
    {
        if (!File.Exists(_settings.DatabasePath))
            return new CrawlerStatusDto { Status = "idle" };

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _settings.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status, started_at, completed_at, mode, target_step,
                   files_scanned, files_processed, files_errored, error_message
            FROM crawl_log
            ORDER BY id DESC
            LIMIT 1
            """;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new CrawlerStatusDto { Status = "idle" };

        var status = reader.GetString(0);
        return new CrawlerStatusDto
        {
            Status = status == "running" ? "running" : "idle",
            StartedAt = reader.IsDBNull(1) ? null : DateTimeOffset.Parse(reader.GetString(1)),
            CompletedAt = reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)),
            Mode = reader.IsDBNull(3) ? null : reader.GetString(3),
            TargetStep = reader.IsDBNull(4) ? null : reader.GetString(4),
            FilesScanned = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
            FilesProcessed = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
            FilesErrored = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
            ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
        };
    }

    public async Task<bool> StartCrawlAsync(StartCrawlRequest request)
    {
        var current = await GetStatusAsync();
        if (current.Status == "running")
            return false;

        var args = BuildArgs(request);
        var psi = new ProcessStartInfo
        {
            FileName = _settings.ExecutablePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        Process.Start(psi);
        return true;
    }

    private string BuildArgs(StartCrawlRequest request)
    {
        var parts = new List<string> { "run", "--mode", request.Mode };
        if (!string.IsNullOrWhiteSpace(request.Step))
        {
            parts.Add("--step");
            parts.Add(request.Step);
        }
        if (!string.IsNullOrWhiteSpace(_settings.ConfigPath))
        {
            parts.Add("--config");
            parts.Add(_settings.ConfigPath);
        }
        return string.Join(' ', parts);
    }
}
