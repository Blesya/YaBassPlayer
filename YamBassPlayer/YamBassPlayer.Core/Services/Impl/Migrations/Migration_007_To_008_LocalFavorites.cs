using Microsoft.Data.Sqlite;
using YamBassPlayer.Services;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Шаг 7→8: локальное избранное возвращается в БД.
/// <list type="bullet">
/// <item>Создаётся таблица <c>favoriteLocalTracks</c> с составным ключом
/// <c>(sourceId, trackId)</c>: в избранное можно класть треки любого источника.</item>
/// <item>Данные из <c>plugin_state/local.json</c> (новый <c>favoriteTracks</c> и старый
/// <c>favoriteTrackIds</c>) переносятся в таблицу; JSON остаётся как резервная копия.</item>
/// </list>
/// </summary>
public sealed class Migration_007_To_008_LocalFavorites(IPluginStateFile pluginState) : IDatabaseMigration
{
	private const string LocalPluginId = "local";
	private const string FavoriteKeysKey = "favoriteTracks";
	private const string LegacyFavoriteIdsKey = "favoriteTrackIds";

	public int FromVersion => 7;

	public int ToVersion => 8;

	public string Description => "Локальное избранное в таблице";

	public void Up(SqliteConnection connection, SqliteTransaction transaction)
	{
		Execute(
			connection,
			transaction,
			"""
			CREATE TABLE IF NOT EXISTS favoriteLocalTracks (
				sourceId TEXT NOT NULL,
				trackId  TEXT NOT NULL,
				addedAt  TEXT NOT NULL,
				PRIMARY KEY (sourceId, trackId)
			);
			""");

		ImportFromState(connection, transaction);
	}

	private void ImportFromState(SqliteConnection connection, SqliteTransaction transaction)
	{
		List<(string SourceId, string TrackId)> keys = ReadKeys();
		if (keys.Count == 0)
			return;

		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText =
			"""
			INSERT OR IGNORE INTO favoriteLocalTracks (sourceId, trackId, addedAt)
			VALUES (@sourceId, @trackId, @addedAt);
			""";
		command.Parameters.Add("@sourceId", SqliteType.Text);
		command.Parameters.Add("@trackId", SqliteType.Text);
		command.Parameters.Add("@addedAt", SqliteType.Text);

		DateTime now = DateTime.UtcNow;
		for (int index = 0; index < keys.Count; index++)
		{
			command.Parameters["@sourceId"].Value = keys[index].SourceId;
			command.Parameters["@trackId"].Value = keys[index].TrackId;
			// Новые сверху: первый ключ в JSON — самый свежий.
			command.Parameters["@addedAt"].Value = now.AddMilliseconds(-index).ToString("O");
			command.ExecuteNonQuery();
		}
	}

	private List<(string SourceId, string TrackId)> ReadKeys()
	{
		var keys = new List<(string SourceId, string TrackId)>();

		foreach (string value in pluginState.Get<List<string>>(LocalPluginId, FavoriteKeysKey) ?? [])
		{
			int separator = value.IndexOf('|');
			if (separator <= 0 || separator == value.Length - 1)
				continue;

			keys.Add((value[..separator], value[(separator + 1)..]));
		}

		if (keys.Count > 0)
			return keys;

		foreach (string trackId in pluginState.Get<List<string>>(LocalPluginId, LegacyFavoriteIdsKey) ?? [])
		{
			if (!string.IsNullOrWhiteSpace(trackId))
				keys.Add(("local", trackId));
		}

		return keys;
	}

	private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}
}
