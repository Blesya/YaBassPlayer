using Microsoft.Data.Sqlite;
using YamBassPlayer.Enums;

namespace YamBassPlayer.Services.Impl;

public sealed class HistoryService : IHistoryService
{
	private const int CurrentSchemaVersion = 5;
	private static readonly string[] RecommendationSources =
	[
		ListenSource.Regular.ToString(),
		ListenSource.OnSameWave.ToString()
	];

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

		ApplyMigrations();
	}

	private void ApplyMigrations()
	{
		int schemaVersion = GetSchemaVersion();

		if (schemaVersion < 1)
		{
			MigrateToVersion1();
			SetSchemaVersion(1);
			schemaVersion = 1;
		}

		if (schemaVersion < 2)
		{
			MigrateToVersion2();
			SetSchemaVersion(2);
			schemaVersion = 2;
		}

		if (schemaVersion < 3)
		{
			MigrateToVersion3();
			SetSchemaVersion(3);
			schemaVersion = 3;
		}

		if (schemaVersion < 4)
		{
			MigrateToVersion4();
			SetSchemaVersion(4);
			schemaVersion = 4;
		}

		if (schemaVersion < 5)
		{
			MigrateToVersion5();
			SetSchemaVersion(5);
			schemaVersion = 5;
		}

		if (schemaVersion != CurrentSchemaVersion)
		{
			throw new InvalidOperationException(
				$"Unsupported listensHistory schema version: {schemaVersion}. Expected {CurrentSchemaVersion}.");
		}
	}

	private int GetSchemaVersion()
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "PRAGMA user_version;";
		return Convert.ToInt32(cmd.ExecuteScalar());
	}

	private void SetSchemaVersion(int version)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = $"PRAGMA user_version = {version};";
		cmd.ExecuteNonQuery();
	}

	private void MigrateToVersion1()
	{
		if (HasColumn("listensHistory", "source"))
			return;

		using var alterCmd = _connection.CreateCommand();
		alterCmd.CommandText =
			"""
			ALTER TABLE listensHistory ADD COLUMN source TEXT NOT NULL DEFAULT 'Regular';
			""";
		alterCmd.ExecuteNonQuery();
	}

	private void MigrateToVersion2()
	{
		// Extend Tracks table with new columns — guard each with HasColumn to survive re-runs.
		var newTrackColumns = new[]
		{
			("DurationMs", "INTEGER"),
			("Year",        "INTEGER"),
			("CoverUrl",    "TEXT"),
			("Genres",      "TEXT"),
			("AlbumId",     "TEXT"),
			("SourceType",  "TEXT DEFAULT 'yandex'"),
		};

		foreach (var (column, definition) in newTrackColumns)
		{
			if (HasColumn("Tracks", column))
				continue;

			using var alterCmd = _connection.CreateCommand();
			alterCmd.CommandText = $"ALTER TABLE Tracks ADD COLUMN {column} {definition};";
			alterCmd.ExecuteNonQuery();
		}

		// New tables — IF NOT EXISTS guards make these idempotent.
		using var createCmd = _connection.CreateCommand();
		createCmd.CommandText =
			"""
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
			""";
		createCmd.ExecuteNonQuery();
	}

	private void MigrateToVersion3()
	{
		// New table for locally scanned music folders.
		using var createCmd = _connection.CreateCommand();
		createCmd.CommandText =
			"""
			CREATE TABLE IF NOT EXISTS LocalFolders (
			    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
			    Path         TEXT    UNIQUE NOT NULL,
			    Name         TEXT    NOT NULL,
			    AddedAt      INTEGER NOT NULL,
			    LastScannedAt INTEGER
			);
			""";
		createCmd.ExecuteNonQuery();

		// Link each track back to its source folder.
		if (!HasColumn("Tracks", "FolderId"))
		{
			using var alterCmd = _connection.CreateCommand();
			alterCmd.CommandText = "ALTER TABLE Tracks ADD COLUMN FolderId INTEGER;";
			alterCmd.ExecuteNonQuery();
		}
	}

	private void MigrateToVersion4()
	{
		var newTrackColumns = new[]
		{
			("SourceTrackId", "TEXT"),
			("LocalFilePath", "TEXT"),
		};

		foreach (var (column, definition) in newTrackColumns)
		{
			if (HasColumn("Tracks", column))
				continue;

			using var alterCmd = _connection.CreateCommand();
			alterCmd.CommandText = $"ALTER TABLE Tracks ADD COLUMN {column} {definition};";
			alterCmd.ExecuteNonQuery();
		}

		using var backfillCmd = _connection.CreateCommand();
		backfillCmd.CommandText =
			"""
			UPDATE Tracks
			SET SourceTrackId = COALESCE(NULLIF(SourceTrackId, ''), TrackId);

			UPDATE Tracks
			SET LocalFilePath = TrackId
			WHERE COALESCE(SourceType, 'yandex') = 'local'
			  AND (LocalFilePath IS NULL OR LocalFilePath = '');
			""";
		backfillCmd.ExecuteNonQuery();
	}

	private void MigrateToVersion5()
	{
		var newTrackColumns = new[]
		{
			("RemoteCoverUrl", "TEXT"),
			("LocalCoverPath", "TEXT"),
		};

		foreach (var (column, definition) in newTrackColumns)
		{
			if (HasColumn("Tracks", column))
				continue;

			using var alterCmd = _connection.CreateCommand();
			alterCmd.CommandText = $"ALTER TABLE Tracks ADD COLUMN {column} {definition};";
			alterCmd.ExecuteNonQuery();
		}

		using var backfillCmd = _connection.CreateCommand();
		backfillCmd.CommandText =
			"""
			UPDATE Tracks
			SET RemoteCoverUrl = COALESCE(NULLIF(RemoteCoverUrl, ''), CoverUrl)
			WHERE COALESCE(SourceType, 'yandex') <> 'local'
			  AND CoverUrl IS NOT NULL
			  AND CoverUrl <> '';

			UPDATE Tracks
			SET LocalCoverPath = COALESCE(NULLIF(LocalCoverPath, ''), CoverUrl)
			WHERE COALESCE(SourceType, 'yandex') = 'local'
			  AND CoverUrl IS NOT NULL
			  AND CoverUrl <> '';
			""";
		backfillCmd.ExecuteNonQuery();
	}

	private bool HasColumn(string tableName, string columnName)
	{
		using var checkCmd = _connection.CreateCommand();
		checkCmd.CommandText = $"PRAGMA table_info({tableName});";
		using var reader = checkCmd.ExecuteReader();
		while (reader.Read())
		{
			if (reader.GetString(1) == columnName)
				return true;
		}

		return false;
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

	public int GetListenHistoryCount()
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = CreateRecommendationSourcesCountQuery();
		AddRecommendationSourceParameters(cmd);
		return Convert.ToInt32(cmd.ExecuteScalar());
	}

	public IReadOnlyList<(string trackId, DateTime utcTime)> GetListenHistory()
	{
		var result = new List<(string trackId, DateTime utcTime)>();

		using var cmd = _connection.CreateCommand();
		cmd.CommandText = $"""
			SELECT trackId, utcTime
			FROM listensHistory
			WHERE source IN ({CreateRecommendationSourcePlaceholders()})
			ORDER BY utcTime
			""";
		AddRecommendationSourceParameters(cmd);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			string trackId = reader.GetString(0);
			string utcTimeStr = reader.GetString(1);
			if (DateTime.TryParse(utcTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utcTime))
				result.Add((trackId, utcTime));
		}

		return result;
	}

	public HashSet<string> GetRecentlyPlayedTrackIds(TimeSpan lookback)
	{
		var result = new HashSet<string>();
		var threshold = DateTime.UtcNow - lookback;

		using var cmd = _connection.CreateCommand();
		cmd.CommandText = $"""
			SELECT DISTINCT trackId
			FROM listensHistory
			WHERE source IN ({CreateRecommendationSourcePlaceholders()}) AND utcTime >= $threshold
			""";
		AddRecommendationSourceParameters(cmd);
		cmd.Parameters.AddWithValue("$threshold", threshold.ToString("O"));

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
			result.Add(reader.GetString(0));

		return result;
	}

	private static string CreateRecommendationSourcesCountQuery()
		=> $"""
			SELECT COUNT(*)
			FROM listensHistory
			WHERE source IN ({CreateRecommendationSourcePlaceholders()})
			""";

	private static string CreateRecommendationSourcePlaceholders()
		=> string.Join(", ", RecommendationSources.Select((_, i) => $"$source{i}"));

	private static void AddRecommendationSourceParameters(SqliteCommand cmd)
	{
		for (int i = 0; i < RecommendationSources.Length; i++)
			cmd.Parameters.AddWithValue($"$source{i}", RecommendationSources[i]);
	}
}
