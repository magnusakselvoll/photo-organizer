using Microsoft.Data.Sqlite;

namespace PhotoOrganizer.Crawler.Data;

public sealed class CrawlerDatabase
{
    private const string Ddl = """
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS schema_version (
            version     INTEGER NOT NULL,
            applied_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
        );

        CREATE TABLE IF NOT EXISTS crawled_files (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            file_path       TEXT    NOT NULL UNIQUE,
            file_hash       TEXT,
            modified_at     TEXT,
            first_seen_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
            last_crawled_at TEXT,
            deleted         INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS step_runs (
            file_id       INTEGER NOT NULL REFERENCES crawled_files(id) ON DELETE CASCADE,
            step_name     TEXT    NOT NULL,
            step_version  INTEGER NOT NULL,
            completed_at  TEXT    NOT NULL,
            status        TEXT    NOT NULL DEFAULT 'completed',
            error_message TEXT,
            PRIMARY KEY (file_id, step_name)
        );

        CREATE TABLE IF NOT EXISTS crawl_log (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            started_at      TEXT    NOT NULL,
            completed_at    TEXT,
            mode            TEXT    NOT NULL,
            target_step     TEXT,
            status          TEXT    NOT NULL DEFAULT 'running',
            files_scanned   INTEGER NOT NULL DEFAULT 0,
            files_processed INTEGER NOT NULL DEFAULT 0,
            files_errored   INTEGER NOT NULL DEFAULT 0,
            error_message   TEXT
        );
        """;

    private readonly string _connectionString;

    public CrawlerDatabase(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_version'";
        var exists = (long)(checkCmd.ExecuteScalar() ?? 0L) > 0;
        if (exists)
            return;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = Ddl;
        cmd.ExecuteNonQuery();

        using var insertVersion = connection.CreateCommand();
        insertVersion.CommandText = "INSERT INTO schema_version (version) VALUES (1)";
        insertVersion.ExecuteNonQuery();
    }
}
