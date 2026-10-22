using Microsoft.Data.Sqlite;
using YamBassPlayer.Services.Impl.Migrations;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Owns all SQLite schema creation and migration. Every table, column, index and backfill that
/// used to live in the individual service constructors is applied here in a single place, so
/// services can be constructed before or after each other without racing on schema creation.
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
	private const int CurrentSchemaVersion = 8;

	private readonly IDbConnectionFactory _connectionFactory;
	private readonly DatabaseMigrationRunner _migrationRunner;

	public DatabaseInitializer(IDbConnectionFactory connectionFactory, IPluginStateFile pluginState)
	{
		_connectionFactory = connectionFactory;
		_migrationRunner = new DatabaseMigrationRunner(DatabaseMigrationRegistry.Create(pluginState));
	}

	public void Initialize()
	{
		using var connection = _connectionFactory.Create();

		// Baseline текущей схемы. Все операции идемпотентны: для уже существующей
		// БД они ничего не меняют, для пустой/старой — создают недостающее.
		CreateTracksTable(connection);
		EnsureTrackColumns(connection);
		CreateHistoryTables(connection);
		CreateIndexes(connection);

		// Backfills must run after every column they depend on exists.
		BackfillLocalSourceType(connection);
		SqliteSchemaHelper.BackfillTrackCoverMetadataColumns(connection);

		int version = DatabaseMigrationRunner.GetUserVersion(connection);
		if (version == 0)
		{
			// Чистая БД или БД, созданная до появления версионирования.
			version = CurrentSchemaVersion;
			DatabaseMigrationRunner.SetUserVersion(connection, null, version);
		}

		_migrationRunner.Migrate(connection, version, CurrentSchemaVersion);
	}

	private static void CreateTracksTable(SqliteConnection connection)
	{
		using var cmd = connection.CreateCommand();
		// Та же схема, что получается после миграции 5→6: общее описание в SqliteSchemaHelper.
		cmd.CommandText = $"CREATE TABLE IF NOT EXISTS Tracks ({SqliteSchemaHelper.TracksTableDefinition});";
		cmd.ExecuteNonQuery();
	}

	private static void EnsureTrackColumns(SqliteConnection connection)
	{
		SqliteSchemaHelper.EnsureTrackColumn(connection, "SourceTrackId", "TEXT");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "LocalFilePath", "TEXT");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "RemoteCoverUrl", "TEXT");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "LocalCoverPath", "TEXT");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "DurationMs", "INTEGER");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "Year", "INTEGER");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "CoverUrl", "TEXT");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "Genres", "TEXT");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "AlbumId", "TEXT");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "SourceType", "TEXT DEFAULT 'yandex'");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "FolderId", "INTEGER");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "TrackNumber", "INTEGER");
		SqliteSchemaHelper.EnsureTrackColumn(connection, "Lyrics", "TEXT");
	}

	private static void CreateHistoryTables(SqliteConnection connection)
	{
		using var cmd = connection.CreateCommand();
		// Та же схема, что получается после миграции 6→7.
		cmd.CommandText =
			"""
			CREATE TABLE IF NOT EXISTS listensHistory (
				id               INTEGER PRIMARY KEY AUTOINCREMENT,
				trackId          TEXT    NOT NULL,
				sourceId         TEXT    NOT NULL DEFAULT 'yandex',
				context          TEXT    NOT NULL DEFAULT 'Regular',
				utcTime          TEXT    NOT NULL,
				utcOffsetMinutes INTEGER NOT NULL
			);

			CREATE TABLE IF NOT EXISTS Artists (
				SourceType  TEXT NOT NULL,
				Id          TEXT NOT NULL,
				Name        TEXT NOT NULL,
				CoverUrl    TEXT,
				Description TEXT,
				UpdatedAt   INTEGER,
				PRIMARY KEY (SourceType, Id)
			);

			CREATE TABLE IF NOT EXISTS Albums (
				SourceType TEXT NOT NULL,
				Id         TEXT NOT NULL,
				Title      TEXT NOT NULL,
				Year       INTEGER,
				CoverUrl   TEXT,
				Genre      TEXT,
				TrackCount INTEGER,
				UpdatedAt  INTEGER,
				PRIMARY KEY (SourceType, Id)
			);

			CREATE TABLE IF NOT EXISTS TrackArtists (
				SourceType TEXT NOT NULL,
				TrackId    TEXT NOT NULL,
				ArtistId   TEXT NOT NULL,
				PRIMARY KEY (SourceType, TrackId, ArtistId)
			);

			CREATE TABLE IF NOT EXISTS favoriteLocalTracks (
				sourceId TEXT NOT NULL,
				trackId  TEXT NOT NULL,
				addedAt  TEXT NOT NULL,
				PRIMARY KEY (sourceId, trackId)
			);

			CREATE TABLE IF NOT EXISTS LocalFolders (
			    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
			    Path         TEXT    UNIQUE NOT NULL,
			    Name         TEXT    NOT NULL,
			    AddedAt      INTEGER NOT NULL,
			    LastScannedAt INTEGER
			);
			""";
		cmd.ExecuteNonQuery();
	}

	private static void CreateIndexes(SqliteConnection connection)
	{
		SqliteSchemaHelper.EnsureTableIndex(connection, "idx_history_trackId", "listensHistory", "trackId");
		SqliteSchemaHelper.EnsureTableIndex(connection, "idx_tracks_source_folder", "Tracks", "SourceType, FolderId");
		SqliteSchemaHelper.EnsureTableIndex(connection, "idx_tracks_artist", "Tracks", "Artist");
	}

	/// <summary>
	/// Marks tracks whose TrackId looks like a file path as SourceType='local'.
	/// This handles the case when SourceType column was just added with DEFAULT 'yandex'
	/// but some tracks were originally inserted by local library scanning (TrackId = file path).
	/// </summary>
	private static void BackfillLocalSourceType(SqliteConnection connection)
	{
		if (!SqliteSchemaHelper.HasTable(connection, "Tracks") || !SqliteSchemaHelper.HasColumn(connection, "Tracks", "SourceType"))
			return;

		using var cmd = connection.CreateCommand();
		// Local tracks use file path as TrackId: on Windows "X:\..." , on Unix "/..."
		cmd.CommandText = """
			UPDATE Tracks
			SET SourceType = 'local'
			WHERE (SourceType IS NULL OR SourceType = 'yandex')
			  AND (TrackId LIKE '_:\%' OR TrackId LIKE '/%')
			""";
		cmd.ExecuteNonQuery();
	}
}
