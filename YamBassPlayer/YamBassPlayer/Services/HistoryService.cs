using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services;

public sealed class HistoryService
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
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                trackId  TEXT    NOT NULL,
                utcTime  TEXT    NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void LogListen(string trackId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO listensHistory (trackId, utcTime)
            VALUES ($t, $u);
            """;

        cmd.Parameters.AddWithValue("$t", trackId);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));

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
            string id = reader.GetString(0);
            int count = reader.GetInt32(1);
            result.Add((id, count));
        }

        return result;
    }
}