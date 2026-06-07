using Microsoft.Data.Sqlite;

namespace PhotoOrganizer.Crawler.Data;

/// <summary>
/// Holds a SQLite connection and an open transaction scoped to one file's DB writes.
/// Dispose rolls back if <see cref="Commit"/> was never called.
/// </summary>
public sealed class CrawlFileTransaction : IDisposable
{
    internal SqliteConnection Connection { get; }
    internal SqliteTransaction Transaction { get; }

    internal CrawlFileTransaction(SqliteConnection connection, SqliteTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    public void Commit() => Transaction.Commit();

    public void Dispose()
    {
        Transaction.Dispose();
        Connection.Dispose();
    }
}
