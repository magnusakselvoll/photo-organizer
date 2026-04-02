using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PhotoOrganizer.Application.Crawler;
using PhotoOrganizer.Infrastructure.Crawler;

namespace PhotoOrganizer.Infrastructure.Tests;

[TestClass]
public class CrawlerServiceStatusTests
{
    // Uses an in-memory SQLite DB with the crawl_log schema

    private static SqliteConnection CreateInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE crawl_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at TEXT NOT NULL,
                completed_at TEXT,
                mode TEXT NOT NULL,
                target_step TEXT,
                status TEXT NOT NULL DEFAULT 'running',
                files_scanned INTEGER NOT NULL DEFAULT 0,
                files_processed INTEGER NOT NULL DEFAULT 0,
                files_errored INTEGER NOT NULL DEFAULT 0,
                error_message TEXT
            )
            """;
        cmd.ExecuteNonQuery();
        return connection;
    }

    private static void InsertCrawlLog(SqliteConnection connection, string status, string mode = "full",
        int filesScanned = 0, int filesProcessed = 0)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO crawl_log (started_at, completed_at, mode, status, files_scanned, files_processed)
            VALUES (datetime('now'), datetime('now'), $mode, $status, $scanned, $processed)
            """;
        cmd.Parameters.AddWithValue("$mode", mode);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$scanned", filesScanned);
        cmd.Parameters.AddWithValue("$processed", filesProcessed);
        cmd.ExecuteNonQuery();
    }

    private static CrawlerService CreateService(string dbPath)
    {
        var options = Options.Create(new CrawlerSettings
        {
            DatabasePath = dbPath,
            ExecutablePath = "crawler"
        });
        return new CrawlerService(options);
    }

    [TestMethod]
    public async Task GetStatus_NoDatabaseFile_ReturnsIdle()
    {
        var service = CreateService("/nonexistent/crawler.db");
        var status = await service.GetStatusAsync();
        Assert.AreEqual("idle", status.Status);
    }

    [TestMethod]
    public async Task GetStatus_EmptyTable_ReturnsIdle()
    {
        // Write an empty DB to a temp file
        var path = Path.GetTempFileName();
        try
        {
            using (var conn = new SqliteConnection($"Data Source={path}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE crawl_log (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        started_at TEXT NOT NULL,
                        completed_at TEXT,
                        mode TEXT NOT NULL,
                        target_step TEXT,
                        status TEXT NOT NULL DEFAULT 'running',
                        files_scanned INTEGER NOT NULL DEFAULT 0,
                        files_processed INTEGER NOT NULL DEFAULT 0,
                        files_errored INTEGER NOT NULL DEFAULT 0,
                        error_message TEXT
                    )
                    """;
                cmd.ExecuteNonQuery();
            }

            var service = CreateService(path);
            var status = await service.GetStatusAsync();
            Assert.AreEqual("idle", status.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task GetStatus_LatestRowRunning_ReturnsRunning()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var conn = new SqliteConnection($"Data Source={path}"))
            {
                conn.Open();
                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE crawl_log (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        started_at TEXT NOT NULL,
                        completed_at TEXT,
                        mode TEXT NOT NULL,
                        target_step TEXT,
                        status TEXT NOT NULL DEFAULT 'running',
                        files_scanned INTEGER NOT NULL DEFAULT 0,
                        files_processed INTEGER NOT NULL DEFAULT 0,
                        files_errored INTEGER NOT NULL DEFAULT 0,
                        error_message TEXT
                    )
                    """;
                createCmd.ExecuteNonQuery();
                InsertCrawlLog(conn, "completed");
                InsertCrawlLog(conn, "running", filesScanned: 10);
            }

            var service = CreateService(path);
            var status = await service.GetStatusAsync();
            Assert.AreEqual("running", status.Status);
            Assert.AreEqual(10, status.FilesScanned);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task GetStatus_LatestRowCompleted_ReturnsIdle()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var conn = new SqliteConnection($"Data Source={path}"))
            {
                conn.Open();
                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE crawl_log (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        started_at TEXT NOT NULL,
                        completed_at TEXT,
                        mode TEXT NOT NULL,
                        target_step TEXT,
                        status TEXT NOT NULL DEFAULT 'running',
                        files_scanned INTEGER NOT NULL DEFAULT 0,
                        files_processed INTEGER NOT NULL DEFAULT 0,
                        files_errored INTEGER NOT NULL DEFAULT 0,
                        error_message TEXT
                    )
                    """;
                createCmd.ExecuteNonQuery();
                InsertCrawlLog(conn, "running");
                InsertCrawlLog(conn, "completed", filesScanned: 5, filesProcessed: 5);
            }

            var service = CreateService(path);
            var status = await service.GetStatusAsync();
            Assert.AreEqual("idle", status.Status);
            Assert.AreEqual(5, status.FilesScanned);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
