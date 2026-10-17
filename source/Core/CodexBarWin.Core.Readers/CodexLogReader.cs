using Microsoft.Data.Sqlite;

namespace CodexBarWin.Core.Readers;

public sealed class CodexLogReader
{
    private readonly string _dbPath;

    public CodexLogReader(string dbPath)
    {
        _dbPath = dbPath;
    }

    public CodexLogEntry? ReadLatestRateLimit()
    {
        using SqliteConnection sqliteConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        sqliteConnection.Open();
        using (SqliteCommand sqliteCommand = sqliteConnection.CreateCommand())
        {
            sqliteCommand.CommandText = "PRAGMA query_only = 1;";
            sqliteCommand.ExecuteNonQuery();
        }
        using SqliteCommand sqliteCommand2 = sqliteConnection.CreateCommand();
        sqliteCommand2.CommandText = "SELECT feedback_log_body, ts FROM logs WHERE feedback_log_body LIKE '%rate_limits%' AND feedback_log_body LIKE '%used_percent%' ORDER BY id DESC LIMIT 1;";
        using SqliteDataReader sqliteDataReader = sqliteCommand2.ExecuteReader();
        if (!sqliteDataReader.Read())
        {
            return null;
        }
        string text = (sqliteDataReader.IsDBNull(0) ? null : sqliteDataReader.GetString(0));
        if (text == null)
        {
            return null;
        }
        long? timestampUnix = (sqliteDataReader.IsDBNull(1) ? ((long?)null) : new long?(sqliteDataReader.GetInt64(1)));
        return new CodexLogEntry(text, timestampUnix);
    }
}
