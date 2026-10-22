using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Шаг 5→6: составная идентичность трека. Заполняет <c>SourceType</c> у строк без источника
/// (укоренённый путь — локальный трек, остальное — Яндекс) и пересобирает <c>Tracks</c>
/// с <c>UNIQUE(SourceType, TrackId)</c> вместо глобально уникального <c>TrackId</c>:
/// SQLite не меняет ограничения через <c>ALTER</c>.
/// </summary>
public sealed class Migration_005_To_006_CompositeTrackIdentity : IDatabaseMigration
{
	public int FromVersion => 5;

	public int ToVersion => 6;

	public string Description => "Составная идентичность трека";

	public void Up(SqliteConnection connection, SqliteTransaction transaction)
	{
		if (!SqliteSchemaHelper.HasTable(connection, "Tracks"))
			return;

		BackfillSourceType(connection, transaction);

		// Идемпотентность: пересобираем только таблицу без нужного уникального индекса.
		if (SqliteSchemaHelper.HasUniqueIndex(connection, "Tracks", "SourceType", "TrackId"))
			return;

		RebuildTracks(connection, transaction);
	}

	/// <summary>
	/// Проставляет источник строкам, у которых он не задан. Укоренённый путь в <c>TrackId</c>
	/// означает локальный трек, всё остальное пришло из Яндекса.
	/// </summary>
	private static void BackfillSourceType(SqliteConnection connection, SqliteTransaction transaction)
	{
		Execute(
			connection,
			transaction,
			"""
			UPDATE Tracks
			SET SourceType = CASE
				WHEN TrackId LIKE '_:\%' OR TrackId LIKE '/%' OR TrackId LIKE '\\%' THEN 'local'
				ELSE 'yandex'
			END
			WHERE SourceType IS NULL OR SourceType = '';
			""");
	}

	private static void RebuildTracks(SqliteConnection connection, SqliteTransaction transaction)
	{
		string[] oldColumns = ReadColumnNames(connection, transaction, "Tracks");

		Execute(connection, transaction, $"CREATE TABLE Tracks_new ({SqliteSchemaHelper.TracksTableDefinition});");

		// Копируем только те колонки, которые есть в старой таблице, — на случай,
		// если БД отстала от baseline и часть колонок ещё не добавлена.
		string[] newColumns = ReadColumnNames(connection, transaction, "Tracks_new");
		string[] columns = newColumns
			.Where(column => oldColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
			.ToArray();
		string columnList = string.Join(", ", columns);

		Execute(connection, transaction, $"INSERT INTO Tracks_new ({columnList}) SELECT {columnList} FROM Tracks;");
		Execute(connection, transaction, "DROP TABLE Tracks;");
		Execute(connection, transaction, "ALTER TABLE Tracks_new RENAME TO Tracks;");

		// Индексы удалённой таблицы уходят вместе с ней — пересоздаём на новой.
		Execute(connection, transaction, "CREATE INDEX IF NOT EXISTS idx_tracks_source_folder ON Tracks(SourceType, FolderId);");
		Execute(connection, transaction, "CREATE INDEX IF NOT EXISTS idx_tracks_artist ON Tracks(Artist);");
	}

	private static string[] ReadColumnNames(SqliteConnection connection, SqliteTransaction transaction, string tableName)
	{
		List<string> columns = [];

		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = $"PRAGMA table_info({tableName});";
		using var reader = command.ExecuteReader();
		while (reader.Read())
			columns.Add(reader.GetString(1));

		return columns.ToArray();
	}

	private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}
}
