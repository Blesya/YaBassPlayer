using Microsoft.Data.Sqlite;
using YamBassPlayer.Enums;

namespace YamBassPlayer.Services.Impl;

public sealed class HistoryService : IHistoryService
{
	private const int CurrentSchemaVersion = 5;

	private readonly SqliteConnection _connection;
	private readonly IDbWriteLock _writeLock;

	public HistoryService(SqliteConnection connection, IDbWriteLock writeLock)
	{
		_connection = connection;
		_writeLock = writeLock;
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
				utcOffsetMinutes INTEGER NOT NULL,
				source           TEXT    NOT NULL DEFAULT 'Regular'
			);

			CREATE TABLE IF NOT EXISTS Artists (
				Id          TEXT PRIMARY KEY,
				Name        TEXT NOT NULL,
				CoverUrl    TEXT,
				Description TEXT,
				UpdatedAt   INTEGER
			);

			CREATE TABLE IF NOT EXISTS Albums (
				Id          TEXT PRIMARY KEY,
				Title       TEXT NOT NULL,
				Year        INTEGER,
				CoverUrl    TEXT,
				Genre       TEXT,
				TrackCount  INTEGER,
				UpdatedAt   INTEGER
			);

			CREATE TABLE IF NOT EXISTS TrackArtists (
				TrackId  TEXT NOT NULL,
				ArtistId TEXT NOT NULL,
				PRIMARY KEY (TrackId, ArtistId)
			);

			CREATE TABLE IF NOT EXISTS LocalFolders (
			    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
			    Path         TEXT    UNIQUE NOT NULL,
			    Name         TEXT    NOT NULL,
			    AddedAt      INTEGER NOT NULL,
			    LastScannedAt INTEGER
			);

			CREATE INDEX IF NOT EXISTS idx_history_trackId ON listensHistory(trackId);
			""";
		createCmd.ExecuteNonQuery();

		SetSchemaVersion(CurrentSchemaVersion);
	}

	private void SetSchemaVersion(int version)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = $"PRAGMA user_version = {version};";
		cmd.ExecuteNonQuery();
	}

	public void LogListen(string trackId, ListenSource source)
	{
		var utcNow = DateTime.UtcNow;
		var offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;

		var lockHandle = _writeLock.AcquireAsync().GetAwaiter().GetResult();
		try
		{
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
		finally
		{
			lockHandle.Dispose();
		}
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
