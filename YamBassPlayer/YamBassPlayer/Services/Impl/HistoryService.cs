using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services.Impl;

public sealed class HistoryService : IHistoryService
{
    private readonly SqliteConnection _connection;

    public HistoryService(SqliteConnection connection)
    {
        _connection = connection;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS listensHistory (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                trackId          TEXT    NOT NULL,
                utcTime          TEXT    NOT NULL,
                utcOffsetMinutes INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void LogListen(string trackId)
    {
        var utcNow = DateTime.UtcNow;
        var offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO listensHistory (trackId, utcTime, utcOffsetMinutes)
            VALUES ($t, $u, $o);
            """;

        cmd.Parameters.AddWithValue("$t", trackId);
        cmd.Parameters.AddWithValue("$u", utcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$o", offset);

        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<(string trackId, int count)> GetTopTracks(int limit = 10)
    {
        var result = new List<(string trackId, int count)>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT trackId, COUNT(*) as cnt
            FROM listensHistory
            GROUP BY trackId
            ORDER BY cnt DESC
            LIMIT $limit;
            """;

        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        return result;
    }
}
