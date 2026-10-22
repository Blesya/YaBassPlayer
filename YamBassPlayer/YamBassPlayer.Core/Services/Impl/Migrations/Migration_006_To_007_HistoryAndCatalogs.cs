using Microsoft.Data.Sqlite;
using YamBassPlayer.Services;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Шаг 6→7: история и справочники по источнику.
/// <list type="bullet">
/// <item><c>listensHistory</c> получает независимые <c>sourceId</c> и <c>context</c>
/// (колонка <c>source</c> переименовывается), контекст нормализуется.</item>
/// <item><c>Artists</c>, <c>Albums</c> и <c>TrackArtists</c> пересобираются с <c>SourceType</c>
/// в ключах: идентификаторы исполнителей и альбомов локальны для источника.</item>
/// <item><c>favoriteLocalTracks</c> переливается в <c>plugin_state/local.json</c> до удаления
/// таблицы — состояние локального плагина становится владельцем избранного.</item>
/// </list>
/// Все шаги идемпотентны: повторный прогон на готовой схеме ничего не меняет.
/// </summary>
public sealed class Migration_006_To_007_HistoryAndCatalogs(IPluginStateFile pluginState) : IDatabaseMigration
{
	private const string LocalPluginId = "local";
	private const string FavoriteTrackIdsKey = "favoriteTrackIds";

	/// <summary>
	/// Признак локального трека по идентификатору: у локальных треков это укоренённый путь.
	/// То же правило использует миграция 5→6, поэтому источник определяется одинаково.
	/// </summary>
	private const string LocalTrackIdPattern =
		"TrackId LIKE '_:\\%' OR TrackId LIKE '/%' OR TrackId LIKE '\\%'";

	public int FromVersion => 6;

	public int ToVersion => 7;

	public string Description => "История и справочники по источнику";

	public void Up(SqliteConnection connection, SqliteTransaction transaction)
	{
		MigrateHistory(connection, transaction);
		RebuildCatalogs(connection, transaction);
		MoveLocalFavorites(connection, transaction);
	}

	// ---------------------------------------------------------------------
	// listensHistory: source → context + sourceId
	// ---------------------------------------------------------------------

	private static void MigrateHistory(SqliteConnection connection, SqliteTransaction transaction)
	{
		if (!SqliteSchemaHelper.HasTable(connection, "listensHistory"))
			return;

		bool hasSource = SqliteSchemaHelper.HasColumn(connection, "listensHistory", "source");
		bool hasContext = SqliteSchemaHelper.HasColumn(connection, "listensHistory", "context");

		if (hasSource && !hasContext)
		{
			Execute(connection, transaction, "ALTER TABLE listensHistory RENAME COLUMN source TO context;");
		}

		// Таблица без обеих колонок контекста — повреждённое/нестандартное состояние:
		// доводим её до целевой схемы, а не падаем на нормализации.
		if (!SqliteSchemaHelper.HasColumn(connection, "listensHistory", "context"))
		{
			Execute(
				connection,
				transaction,
				"ALTER TABLE listensHistory ADD COLUMN context TEXT NOT NULL DEFAULT 'Regular';");
		}

		if (!SqliteSchemaHelper.HasColumn(connection, "listensHistory", "sourceId"))
		{
			Execute(
				connection,
				transaction,
				"ALTER TABLE listensHistory ADD COLUMN sourceId TEXT NOT NULL DEFAULT 'yandex';");
		}

		BackfillHistorySourceId(connection, transaction);
		NormalizeContext(connection, transaction);
	}

	/// <summary>
	/// Проставляет источник прослушивания. Сначала — по признаку пути (локальный трек хранит
	/// путь в <c>trackId</c>). Затем источник уточняется по <c>Tracks</c>, но только если
	/// идентификатор однозначно принадлежит одному источнику: при реальном дубле
	/// <c>TrackId</c> между источниками произвольная строка не выбирается.
	/// </summary>
	private static void BackfillHistorySourceId(SqliteConnection connection, SqliteTransaction transaction)
	{
		Execute(
			connection,
			transaction,
			$"""
			UPDATE listensHistory
			SET sourceId = CASE WHEN {LocalTrackIdPattern} THEN 'local' ELSE 'yandex' END;
			""");

		if (!SqliteSchemaHelper.HasTable(connection, "Tracks")
			|| !SqliteSchemaHelper.HasColumn(connection, "Tracks", "SourceType"))
			return;

		Execute(
			connection,
			transaction,
			"""
			UPDATE listensHistory
			SET sourceId = (SELECT MIN(t.SourceType) FROM Tracks t WHERE t.TrackId = listensHistory.trackId)
			WHERE (SELECT COUNT(DISTINCT t.SourceType) FROM Tracks t WHERE t.TrackId = listensHistory.trackId) = 1;
			""");
	}

	private static void NormalizeContext(SqliteConnection connection, SqliteTransaction transaction)
	{
		Execute(
			connection,
			transaction,
			"UPDATE listensHistory SET context = 'Radio' WHERE context = 'MyWave';");

		Execute(
			connection,
			transaction,
			"""
			UPDATE listensHistory
			SET context = 'Regular'
			WHERE context IS NULL OR context NOT IN ('Regular', 'Radio');
			""");
	}

	// ---------------------------------------------------------------------
	// Справочники: SourceType в ключах
	// ---------------------------------------------------------------------

	private static void RebuildCatalogs(SqliteConnection connection, SqliteTransaction transaction)
	{
		if (!SqliteSchemaHelper.HasTable(connection, "Tracks"))
			return;

		// Порядок важен: Artists ссылается на уже пересобранный TrackArtists, а тот —
		// на Tracks, поэтому источник ссылки на исполнителя известен до пересборки Artists.
		if (SqliteSchemaHelper.HasTable(connection, "TrackArtists")
			&& !SqliteSchemaHelper.HasUniqueIndex(connection, "TrackArtists", "SourceType", "TrackId", "ArtistId"))
		{
			RebuildTrackArtists(connection, transaction);
		}

		if (SqliteSchemaHelper.HasTable(connection, "Artists")
			&& !SqliteSchemaHelper.HasUniqueIndex(connection, "Artists", "SourceType", "Id"))
		{
			RebuildArtists(connection, transaction);
		}

		if (SqliteSchemaHelper.HasTable(connection, "Albums")
			&& !SqliteSchemaHelper.HasUniqueIndex(connection, "Albums", "SourceType", "Id"))
		{
			RebuildAlbums(connection, transaction);
		}
	}

	/// <summary>
	/// Источник строки <c>TrackArtists</c>. Старая таблица знает только <c>TrackId</c>,
	/// поэтому источник берётся из <c>Tracks</c> лишь при однозначном совпадении, иначе —
	/// по признаку пути. Строки без трека не переносятся: это сироты.
	/// </summary>
	private static void RebuildTrackArtists(SqliteConnection connection, SqliteTransaction transaction)
	{
		Execute(
			connection,
			transaction,
			"""
			CREATE TABLE TrackArtists_new (
				SourceType TEXT NOT NULL,
				TrackId    TEXT NOT NULL,
				ArtistId   TEXT NOT NULL,
				PRIMARY KEY (SourceType, TrackId, ArtistId)
			);
			""");

		Execute(
			connection,
			transaction,
			$"""
			INSERT OR IGNORE INTO TrackArtists_new (SourceType, TrackId, ArtistId)
			SELECT DISTINCT
				CASE
					WHEN (SELECT COUNT(DISTINCT t.SourceType) FROM Tracks t WHERE t.TrackId = ta.TrackId) = 1
						THEN (SELECT MIN(t.SourceType) FROM Tracks t WHERE t.TrackId = ta.TrackId)
					WHEN {LocalTrackIdPattern} THEN 'local'
					ELSE 'yandex'
				END,
				ta.TrackId,
				ta.ArtistId
			FROM TrackArtists ta
			WHERE EXISTS (SELECT 1 FROM Tracks t WHERE t.TrackId = ta.TrackId);
			""");

		Execute(connection, transaction, "DROP TABLE TrackArtists;");
		Execute(connection, transaction, "ALTER TABLE TrackArtists_new RENAME TO TrackArtists;");
	}

	/// <summary>
	/// Пересобирает <c>Artists</c> по ссылкам из уже пересобранного <c>TrackArtists</c>:
	/// один и тот же <c>Id</c> из двух источников даёт две строки. Исполнители без треков
	/// не переносятся — это сироты.
	/// </summary>
	private static void RebuildArtists(SqliteConnection connection, SqliteTransaction transaction)
	{
		Execute(
			connection,
			transaction,
			"""
			CREATE TABLE Artists_new (
				SourceType  TEXT NOT NULL,
				Id          TEXT NOT NULL,
				Name        TEXT NOT NULL,
				CoverUrl    TEXT,
				Description TEXT,
				UpdatedAt   INTEGER,
				PRIMARY KEY (SourceType, Id)
			);
			""");

		Execute(
			connection,
			transaction,
			"""
			INSERT OR IGNORE INTO Artists_new (SourceType, Id, Name, CoverUrl, Description, UpdatedAt)
			SELECT DISTINCT ta.SourceType, a.Id, a.Name, a.CoverUrl, a.Description, a.UpdatedAt
			FROM Artists a
			JOIN TrackArtists ta ON ta.ArtistId = a.Id;
			""");

		Execute(connection, transaction, "DROP TABLE Artists;");
		Execute(connection, transaction, "ALTER TABLE Artists_new RENAME TO Artists;");
	}

	/// <summary>
	/// Пересобирает <c>Albums</c> по ссылкам из <c>Tracks</c>: источник альбома — источник
	/// трека, который на него ссылается. Один <c>Id</c> из двух источников даёт две строки,
	/// альбомы без треков (сироты) не переносятся.
	/// </summary>
	private static void RebuildAlbums(SqliteConnection connection, SqliteTransaction transaction)
	{
		Execute(
			connection,
			transaction,
			"""
			CREATE TABLE Albums_new (
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
			""");

		Execute(
			connection,
			transaction,
			$"""
			INSERT OR IGNORE INTO Albums_new (SourceType, Id, Title, Year, CoverUrl, Genre, TrackCount, UpdatedAt)
			SELECT DISTINCT
				COALESCE(t.SourceType, CASE WHEN {LocalTrackIdPattern} THEN 'local' ELSE 'yandex' END),
				al.Id, al.Title, al.Year, al.CoverUrl, al.Genre, al.TrackCount, al.UpdatedAt
			FROM Albums al
			JOIN Tracks t ON t.AlbumId = al.Id;
			""");

		Execute(connection, transaction, "DROP TABLE Albums;");
		Execute(connection, transaction, "ALTER TABLE Albums_new RENAME TO Albums;");
	}

	// ---------------------------------------------------------------------
	// favoriteLocalTracks → plugin_state/local.json
	// ---------------------------------------------------------------------

	/// <summary>
	/// Переносит локальное избранное в состояние плагина <b>до</b> удаления таблицы.
	/// Порядок — как в таблице (<c>ORDER BY addedAt DESC</c>); уже записанные в состоянии
	/// идентификаторы сохраняются и не дублируются, поэтому повторный прогон безопасен.
	/// </summary>
	private void MoveLocalFavorites(SqliteConnection connection, SqliteTransaction transaction)
	{
		if (!SqliteSchemaHelper.HasTable(connection, "favoriteLocalTracks"))
			return;

		List<string> fromTable = ReadLocalFavoriteTrackIds(connection, transaction);
		List<string> fromState = pluginState.Get<List<string>>(LocalPluginId, FavoriteTrackIdsKey) ?? [];

		List<string> merged = [.. fromTable];
		foreach (string trackId in fromState)
		{
			if (!merged.Contains(trackId))
				merged.Add(trackId);
		}

		if (merged.Count > 0)
			pluginState.Set(LocalPluginId, FavoriteTrackIdsKey, merged);

		Execute(connection, transaction, "DROP TABLE favoriteLocalTracks;");
	}

	private static List<string> ReadLocalFavoriteTrackIds(
		SqliteConnection connection,
		SqliteTransaction transaction)
	{
		var trackIds = new List<string>();

		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "SELECT trackId FROM favoriteLocalTracks ORDER BY addedAt DESC;";
		using var reader = command.ExecuteReader();
		while (reader.Read())
			trackIds.Add(reader.GetString(0));

		return trackIds;
	}

	private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}
}
