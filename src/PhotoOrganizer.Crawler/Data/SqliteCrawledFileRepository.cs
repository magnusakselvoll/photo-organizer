using Microsoft.Data.Sqlite;

namespace PhotoOrganizer.Crawler.Data;

public sealed class SqliteCrawledFileRepository : ICrawledFileRepository
{
    private readonly CrawlerDatabase _db;

    public SqliteCrawledFileRepository(CrawlerDatabase db) => _db = db;

    public async Task<CrawledFileRecord?> GetByPathAsync(string filePath)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, file_path, file_hash, modified_at, first_seen_at, last_crawled_at, deleted
            FROM crawled_files WHERE file_path = @path
            """;
        cmd.Parameters.AddWithValue("@path", filePath);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return ReadRecord(reader);
    }

    public async Task<CrawledFileRecord> UpsertAsync(string filePath, string? fileHash, DateTimeOffset modifiedAt)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO crawled_files (file_path, file_hash, modified_at, last_crawled_at, deleted)
            VALUES (@path, @hash, @modifiedAt, @now, 0)
            ON CONFLICT(file_path) DO UPDATE SET
                file_hash = excluded.file_hash,
                modified_at = excluded.modified_at,
                last_crawled_at = excluded.last_crawled_at,
                deleted = 0
            RETURNING id, file_path, file_hash, modified_at, first_seen_at, last_crawled_at, deleted
            """;
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@hash", (object?)fileHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@modifiedAt", modifiedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return ReadRecord(reader);
    }

    public async Task MarkDeletedAsync(IEnumerable<int> fileIds)
    {
        var ids = fileIds.ToList();
        if (ids.Count == 0)
            return;
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"UPDATE crawled_files SET deleted = 1 WHERE id IN ({string.Join(",", ids)})";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateModifiedAtAsync(int fileId, DateTimeOffset modifiedAt)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE crawled_files SET modified_at = @modifiedAt, last_crawled_at = @now WHERE id = @id";
        cmd.Parameters.AddWithValue("@modifiedAt", modifiedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", fileId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<CrawledFileRecord>> GetActiveFilesAsync()
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, file_path, file_hash, modified_at, first_seen_at, last_crawled_at, deleted
            FROM crawled_files WHERE deleted = 0
            """;
        using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<CrawledFileRecord>();
        while (await reader.ReadAsync())
            results.Add(ReadRecord(reader));
        return results;
    }

    public async Task RecordStepRunAsync(int fileId, string stepName, int stepVersion, string status, string? errorMessage)
    {
        using var connection = _db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO step_runs (file_id, step_name, step_version, completed_at, status, error_message)
            VALUES (@fileId, @stepName, @stepVersion, @now, @status, @errorMessage)
            ON CONFLICT(file_id, step_name) DO UPDATE SET
                step_version = excluded.step_version,
                completed_at = excluded.completed_at,
                status = excluded.status,
                error_message = excluded.error_message
            """;
        cmd.Parameters.AddWithValue("@fileId", fileId);
        cmd.Parameters.AddWithValue("@stepName", stepName);
        cmd.Parameters.AddWithValue("@stepVersion", stepVersion);
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static CrawledFileRecord ReadRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        FilePath = reader.GetString(1),
        FileHash = reader.IsDBNull(2) ? null : reader.GetString(2),
        ModifiedAt = reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3)),
        FirstSeenAt = DateTimeOffset.Parse(reader.GetString(4)),
        LastCrawledAt = reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)),
        Deleted = reader.GetInt32(6) == 1
    };
}
