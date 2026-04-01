namespace PhotoOrganizer.Crawler.Data;

public sealed class SqliteCrawlLogRepository : ICrawlLogRepository
{
    private readonly CrawlerDatabase _db;

    public SqliteCrawlLogRepository(CrawlerDatabase db) => _db = db;

    public async Task<int> StartCrawlAsync(string mode, string? targetStep = null)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO crawl_log (started_at, mode, target_step, status)
            VALUES (@now, @mode, @targetStep, 'running')
            RETURNING id
            """;
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@mode", mode);
        cmd.Parameters.AddWithValue("@targetStep", (object?)targetStep ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task CompleteCrawlAsync(int crawlId, string status, int filesScanned, int filesProcessed, int filesErrored, string? errorMessage = null)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE crawl_log SET
                completed_at = @now,
                status = @status,
                files_scanned = @filesScanned,
                files_processed = @filesProcessed,
                files_errored = @filesErrored,
                error_message = @errorMessage
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@filesScanned", filesScanned);
        cmd.Parameters.AddWithValue("@filesProcessed", filesProcessed);
        cmd.Parameters.AddWithValue("@filesErrored", filesErrored);
        cmd.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", crawlId);
        await cmd.ExecuteNonQueryAsync();
    }
}
