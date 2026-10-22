using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Centralizes SQLite schema operations: column existence checks, adding columns,
/// creating indexes, and detecting table existence. Shared across services to eliminate
/// duplicated schema management code.
/// </summary>
public static class SqliteSchemaHelper
{
	/// <summary>
	/// Каноническое описание таблицы <c>Tracks</c> версии 6: используется и при создании
	/// чистой БД, и при пересборке таблицы миграцией 5→6, чтобы обе схемы совпадали.
	/// </summary>
	internal const string TracksTableDefinition =
		"""
		Id INTEGER PRIMARY KEY AUTOINCREMENT,
		TrackId TEXT,
		Artist TEXT,
		Title TEXT,
		Album TEXT,
		RemoteCoverUrl TEXT,
		LocalCoverPath TEXT,
		SourceTrackId TEXT,
		LocalFilePath TEXT,
		UpdatedAt INTEGER,
		DurationMs INTEGER,
		Year INTEGER,
		CoverUrl TEXT,
		Genres TEXT,
		AlbumId TEXT,
		SourceType TEXT DEFAULT 'yandex',
		FolderId INTEGER,
		TrackNumber INTEGER,
		Lyrics TEXT,
		UNIQUE(SourceType, TrackId)
		""";

	/// <summary>Returns true if the given table exists in the database.</summary>
	public static bool HasTable(SqliteConnection connection, string tableName)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @tableName LIMIT 1;";
		cmd.Parameters.AddWithValue("@tableName", tableName);
		return cmd.ExecuteScalar() is not null;
	}

	/// <summary>Returns true if the given column exists in the specified table.</summary>
	public static bool HasColumn(SqliteConnection connection, string tableName, string columnName)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = $"PRAGMA table_info({tableName});";
		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			if (reader.GetString(1) == columnName)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Ensures a column exists on the Tracks table. If the table doesn't exist yet, this is a no-op.
	/// </summary>
	public static void EnsureTrackColumn(SqliteConnection connection, string columnName, string definition)
	{
		if (!HasTable(connection, "Tracks") || HasColumn(connection, "Tracks", columnName))
			return;

		using var cmd = connection.CreateCommand();
		cmd.CommandText = $"ALTER TABLE Tracks ADD COLUMN {columnName} {definition};";
		cmd.ExecuteNonQuery();
	}

	/// <summary>
	/// Ensures an index exists on the specified table and columns.
	/// </summary>
	public static void EnsureTableIndex(SqliteConnection connection, string indexName, string tableName, string columns)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = $"CREATE INDEX IF NOT EXISTS {indexName} ON {tableName}({columns});";
		cmd.ExecuteNonQuery();
	}

	/// <summary>
	/// Returns true when the table has a unique index whose columns are exactly
	/// <paramref name="columns"/>, in the same order. Both UNIQUE constraints and
	/// <c>CREATE UNIQUE INDEX</c> are recognized.
	/// </summary>
	public static bool HasUniqueIndex(SqliteConnection connection, string tableName, params string[] columns)
	{
		if (columns.Length == 0 || !HasTable(connection, tableName))
			return false;

		List<string> uniqueIndexes = [];

		using (var listCmd = connection.CreateCommand())
		{
			listCmd.CommandText = $"PRAGMA index_list({tableName});";
			using var reader = listCmd.ExecuteReader();
			while (reader.Read())
			{
				// Колонки PRAGMA index_list: seq, name, unique, origin, partial.
				if (reader.GetInt64(2) == 1)
					uniqueIndexes.Add(reader.GetString(1));
			}
		}

		foreach (string indexName in uniqueIndexes)
		{
			List<string> indexColumns = [];

			using var infoCmd = connection.CreateCommand();
			infoCmd.CommandText = $"PRAGMA index_info({indexName});";
			using var reader = infoCmd.ExecuteReader();
			while (reader.Read())
			{
				// Колонки PRAGMA index_info: seqno, cid, name.
				indexColumns.Add(reader.IsDBNull(2) ? string.Empty : reader.GetString(2));
			}

			if (indexColumns.SequenceEqual(columns, StringComparer.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Backfills RemoteCoverUrl and LocalCoverPath from the legacy CoverUrl column based on SourceType.
	/// Safe to call multiple times — UPDATE with COALESCE only acts on null/empty target values.
	/// </summary>
	public static void BackfillTrackCoverMetadataColumns(SqliteConnection connection)
	{
		if (!HasTable(connection, "Tracks")
			|| !HasColumn(connection, "Tracks", "CoverUrl")
			|| !HasColumn(connection, "Tracks", "SourceType"))
			return;

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
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
		cmd.ExecuteNonQuery();
	}
}
