using Microsoft.Data.Sqlite;
using YamBassPlayer.Enums;

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
		using var createCmd = _connection.CreateCommand();
		createCmd.CommandText =
			"""
			CREATE TABLE IF NOT EXISTS listensHistory (
				id               INTEGER PRIMARY KEY AUTOINCREMENT,
				trackId          TEXT    NOT NULL,
				utcTime          TEXT    NOT NULL,
				utcOffsetMinutes INTEGER NOT NULL
			);
			""";
		createCmd.ExecuteNonQuery();

		MigrateAddSourceColumn();
	}

	private void MigrateAddSourceColumn()
	{
		using var checkCmd = _connection.CreateCommand();
		checkCmd.CommandText = "PRAGMA table_info(listensHistory);";
		using var reader = checkCmd.ExecuteReader();
		bool hasSource = false;
		while (reader.Read())
		{
			if (reader.GetString(1) == "source")
			{
				hasSource = true;
				break;
			}
		}

		if (hasSource)
			return;

		using var alterCmd = _connection.CreateCommand();
		alterCmd.CommandText =
			"""
			ALTER TABLE listensHistory ADD COLUMN source TEXT NOT NULL DEFAULT 'Regular';
			""";
		alterCmd.ExecuteNonQuery();
	}

	public void LogListen(string trackId, ListenSource source)
	{
		var utcNow = DateTime.UtcNow;
		var offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;

		using var cmd = _connection.CreateCommand();
		cmd.CommandText =
			"""
			INSERT INTO listensHistory (trackId, utcTime, utcOffsetMinutes, source)
			VALUES ($t, $u, $o, $s);
			""";

		cmd.Parameters.AddWithValue("$t", trackId);
		cmd.Parameters.AddWithValue("$u", utcNow.ToString("O"));
		cmd.Parameters.AddWithValue("$o", offset);
		cmd.Parameters.AddWithValue("$s", source.ToString());

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

	public IReadOnlyList<(string trackId, int count)> GetTopEveningTracks(int limit = 10)
	{
		var result = new List<(string trackId, int count)>();

		using var cmd = _connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT trackId, COUNT(*) as cnt
			FROM listensHistory
			WHERE CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) >= 16
			  AND CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) < 24
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

	public IReadOnlyList<(string trackId, int count)> GetTopTracksByDayOfWeek(DayOfWeek day, int limit = 50)
	{
		var result = new List<(string trackId, int count)>();

		using var cmd = _connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT trackId, COUNT(*) as cnt
			FROM listensHistory
			WHERE CAST(strftime('%w', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) = $dayOfWeek
			GROUP BY trackId
			ORDER BY cnt DESC
			LIMIT $limit;
			""";

		cmd.Parameters.AddWithValue("$dayOfWeek", (int)day);
		cmd.Parameters.AddWithValue("$limit", limit);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			result.Add((reader.GetString(0), reader.GetInt32(1)));
		}

		return result;
	}

	public int GetListenCount(string trackId)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COUNT(*) FROM listensHistory WHERE trackId = $trackId;
			""";
		cmd.Parameters.AddWithValue("$trackId", trackId);
		return Convert.ToInt32(cmd.ExecuteScalar());
	}
}
