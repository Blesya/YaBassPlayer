using Microsoft.Data.Sqlite;
using YamBassPlayer.Services;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Шаг 8→9: постоянный состав плейлистов. Нужен, чтобы список треков открывался
/// из локальной БД, не дожидаясь ответа источника: <c>playlistCompositions</c>
/// хранит время сохранения (для чистки), <c>playlistCompositionTracks</c> — id
/// треков по порядку. Ключ — <c>Playlist.CacheKey</c>.
/// </summary>
public sealed class Migration_008_To_009_PlaylistCompositions : IDatabaseMigration
{
	public int FromVersion => 8;

	public int ToVersion => 9;

	public string Description => "Постоянный состав плейлистов";

	public void Up(SqliteConnection connection, SqliteTransaction transaction)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText =
			"""
			CREATE TABLE IF NOT EXISTS playlistCompositions (
				playlistKey TEXT PRIMARY KEY,
				savedAt     TEXT NOT NULL
			);

			CREATE TABLE IF NOT EXISTS playlistCompositionTracks (
				playlistKey TEXT NOT NULL,
				position    INTEGER NOT NULL,
				sourceId    TEXT NOT NULL,
				trackId     TEXT NOT NULL,
				PRIMARY KEY (playlistKey, position)
			);

			CREATE INDEX IF NOT EXISTS idx_playlist_composition_tracks
				ON playlistCompositionTracks(playlistKey);
			""";
		command.ExecuteNonQuery();
	}
}
