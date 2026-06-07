using Microsoft.Data.Sqlite;
using PhotoOrganizer.Crawler.Data;

namespace PhotoOrganizer.Crawler.Tests;

/// <summary>
/// Integration tests verifying that <see cref="CrawlerDatabase.BeginFileTransaction"/> produces
/// a real SQLite transaction that rolls back on dispose and commits when explicitly committed.
/// These tests use a real on-disk temp SQLite database.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class CrawlFileTransactionTests
{
    private static string _dbPath = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"crawler-tx-test-{Guid.NewGuid():N}.db");
        var db = new CrawlerDatabase(_dbPath);
        db.Initialize();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [TestMethod]
    public async Task UpsertAsync_WithTransactionRolledBack_LeavesNoPersistentRow()
    {
        var db = new CrawlerDatabase(_dbPath);
        var repo = new SqliteCrawledFileRepository(db);

        const string filePath = "/test/rollback.jpg";

        // Insert an initial row so we can verify it's untouched after rollback
        var original = await repo.UpsertAsync(filePath, "original-hash", DateTimeOffset.UtcNow);

        // Begin a transaction, update the row within it, then dispose WITHOUT committing
        using (var tx = db.BeginFileTransaction())
        {
            await repo.UpsertAsync(filePath, "new-hash", DateTimeOffset.UtcNow, tx);
            // tx.Commit() deliberately omitted — dispose rolls back
        }

        // The row should still have the original hash
        var after = await repo.GetByPathAsync(filePath);
        Assert.IsNotNull(after);
        Assert.AreEqual("original-hash", after.FileHash, "Rolled-back transaction must not persist the new hash");
    }

    [TestMethod]
    public async Task UpsertAsync_WithTransactionCommitted_PersistsRow()
    {
        var db = new CrawlerDatabase(_dbPath);
        var repo = new SqliteCrawledFileRepository(db);

        const string filePath = "/test/commit.jpg";

        using (var tx = db.BeginFileTransaction())
        {
            await repo.UpsertAsync(filePath, "committed-hash", DateTimeOffset.UtcNow, tx);
            tx.Commit();
        }

        var after = await repo.GetByPathAsync(filePath);
        Assert.IsNotNull(after);
        Assert.AreEqual("committed-hash", after.FileHash, "Committed transaction must persist the hash");
    }

    [TestMethod]
    public async Task RecordStepRunAsync_WithTransactionRolledBack_LeavesNoStepRunRow()
    {
        var db = new CrawlerDatabase(_dbPath);
        var repo = new SqliteCrawledFileRepository(db);

        // Create the file row first (autocommit, so it survives any later rollback)
        const string filePath = "/test/step-rollback.jpg";
        var record = await repo.UpsertAsync(filePath, "hash-sr", DateTimeOffset.UtcNow);

        // Begin a transaction, write a step_runs row, then dispose WITHOUT committing
        using (var tx = db.BeginFileTransaction())
        {
            await repo.RecordStepRunAsync(record.Id, "metadata", 1, "completed", null, tx);
            // tx.Commit() deliberately omitted
        }

        // Verify the step_runs row was not written
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM step_runs WHERE file_id = @id AND step_name = 'metadata'";
        cmd.Parameters.AddWithValue("@id", record.Id);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.AreEqual(0L, count, "Rolled-back transaction must not persist the step_runs row");
    }

    [TestMethod]
    public async Task RecordStepRunAsync_WithTransactionCommitted_PersistsStepRunRow()
    {
        var db = new CrawlerDatabase(_dbPath);
        var repo = new SqliteCrawledFileRepository(db);

        const string filePath = "/test/step-commit.jpg";
        var record = await repo.UpsertAsync(filePath, "hash-sc", DateTimeOffset.UtcNow);

        using (var tx = db.BeginFileTransaction())
        {
            await repo.RecordStepRunAsync(record.Id, "metadata", 1, "completed", null, tx);
            tx.Commit();
        }

        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM step_runs WHERE file_id = @id AND step_name = 'metadata'";
        cmd.Parameters.AddWithValue("@id", record.Id);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.AreEqual(1L, count, "Committed transaction must persist the step_runs row");
    }

    [TestMethod]
    public async Task UpsertAndRecordStepRun_WithinSameTransaction_BothRollBackTogether()
    {
        var db = new CrawlerDatabase(_dbPath);
        var repo = new SqliteCrawledFileRepository(db);

        const string filePath = "/test/combined-rollback.jpg";

        // Put a baseline row in
        var original = await repo.UpsertAsync(filePath, "base-hash", DateTimeOffset.UtcNow);

        // Now update both crawled_files and step_runs within one tx, then roll back
        using (var tx = db.BeginFileTransaction())
        {
            var updated = await repo.UpsertAsync(filePath, "tx-hash", DateTimeOffset.UtcNow, tx);
            await repo.RecordStepRunAsync(updated.Id, "metadata", 1, "completed", null, tx);
            // No Commit — rollback on dispose
        }

        // crawled_files hash unchanged
        var afterFile = await repo.GetByPathAsync(filePath);
        Assert.AreEqual("base-hash", afterFile?.FileHash, "File hash must be rolled back");

        // step_runs row absent
        using var connection = db.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM step_runs WHERE file_id = @id";
        cmd.Parameters.AddWithValue("@id", original.Id);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.AreEqual(0L, count, "step_runs row must be rolled back together with the file row");
    }
}
