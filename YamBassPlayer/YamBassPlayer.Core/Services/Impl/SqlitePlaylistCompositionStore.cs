using Microsoft.Data.Sqlite;
using Serilog;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Состав плейлистов в SQLite (<c>playlistCompositions</c> + <c>playlistCompositionTracks</c>).
/// Чтение одного плейлиста — один индексный запрос; запись идёт в транзакции под
/// <see cref="IDbWriteLock"/>, чтобы не конкурировать с кешем треков.
/// Старые составы вычищаются: хранится не больше <see cref="MaxPlaylists"/> штук.
/// </summary>
public sealed class SqlitePlaylistCompositionStore(
	IDbConnectionFactory connectionFactory,
	IDbWriteLock writeLock) : IPlaylistCompositionStore
{
	/// <summary>Сколько плейлистов помнить. Больше не нужно: остальные читаются из источника.</summary>
	private const int MaxPlaylists = 100;

	public async Task<IReadOnlyList<TrackKey>?> TryLoadAsync(string playlistKey, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(playlistKey))
			return null;

		await using SqliteConnection connection = connectionFactory.Create();
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT sourceId, trackId
			FROM playlistCompositionTracks
			WHERE playlistKey = @key
			ORDER BY position;
			""";
		command.Parameters.AddWithValue("@key", playlistKey);

		var keys = new List<TrackKey>();
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);
		while (await reader.ReadAsync(ct))
			keys.Add(new TrackKey(new SourceId(reader.GetString(0)), reader.GetString(1)));

		return keys.Count == 0 ? null : keys;
	}

	public async Task SaveAsync(
		string playlistKey,
		IReadOnlyList<TrackKey> trackKeys,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(playlistKey) || trackKeys.Count == 0)
			return;

		using IDisposable _ = await writeLock.AcquireAsync(ct);
		await using SqliteConnection connection = connectionFactory.Create();
		await using SqliteTransaction transaction = connection.BeginTransaction();

		await DeleteTracksAsync(connection, transaction, playlistKey, ct);

		await using (SqliteCommand insert = connection.CreateCommand())
		{
			insert.Transaction = transaction;
			insert.CommandText =
				"""
				INSERT INTO playlistCompositionTracks (playlistKey, position, sourceId, trackId)
				VALUES (@key, @position, @sourceId, @trackId);
				""";
			SqliteParameter key = insert.Parameters.Add("@key", SqliteType.Text);
			SqliteParameter position = insert.Parameters.Add("@position", SqliteType.Integer);
			SqliteParameter source = insert.Parameters.Add("@sourceId", SqliteType.Text);
			SqliteParameter track = insert.Parameters.Add("@trackId", SqliteType.Text);
			key.Value = playlistKey;

			for (int index = 0; index < trackKeys.Count; index++)
			{
				position.Value = index;
				source.Value = trackKeys[index].SourceId.Value;
				track.Value = trackKeys[index].TrackId;
				await insert.ExecuteNonQueryAsync(ct);
			}
		}

		await using (SqliteCommand meta = connection.CreateCommand())
		{
			meta.Transaction = transaction;
			meta.CommandText =
				"""
				INSERT INTO playlistCompositions (playlistKey, savedAt)
				VALUES (@key, @savedAt)
				ON CONFLICT(playlistKey) DO UPDATE SET savedAt = excluded.savedAt;
				""";
			meta.Parameters.AddWithValue("@key", playlistKey);
			meta.Parameters.AddWithValue("@savedAt", DateTime.UtcNow.ToString("O"));
			await meta.ExecuteNonQueryAsync(ct);
		}

		await transaction.CommitAsync(ct);
		await PruneAsync(connection, ct);
	}

	public async Task RemoveAsync(IEnumerable<string> playlistKeys, CancellationToken ct = default)
	{
		List<string> keys = playlistKeys
			.Distinct(StringComparer.Ordinal)
			.ToList();
		if (keys.Count == 0)
			return;

		using IDisposable _ = await writeLock.AcquireAsync(ct);
		await using SqliteConnection connection = connectionFactory.Create();
		await using SqliteTransaction transaction = connection.BeginTransaction();

		foreach (string key in keys)
		{
			await DeleteTracksAsync(connection, transaction, key, ct);

			await using SqliteCommand meta = connection.CreateCommand();
			meta.Transaction = transaction;
			meta.CommandText = "DELETE FROM playlistCompositions WHERE playlistKey = @key;";
			meta.Parameters.AddWithValue("@key", key);
			await meta.ExecuteNonQueryAsync(ct);
		}

		await transaction.CommitAsync(ct);
	}

	public async Task<IReadOnlyCollection<string>> GetKeysAsync(CancellationToken ct = default)
	{
		await using SqliteConnection connection = connectionFactory.Create();
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT playlistKey FROM playlistCompositions;";

		var keys = new List<string>();
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);
		while (await reader.ReadAsync(ct))
			keys.Add(reader.GetString(0));

		return keys;
	}

	private static async Task DeleteTracksAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		string playlistKey,
		CancellationToken ct)
	{
		await using SqliteCommand delete = connection.CreateCommand();
		delete.Transaction = transaction;
		delete.CommandText = "DELETE FROM playlistCompositionTracks WHERE playlistKey = @key;";
		delete.Parameters.AddWithValue("@key", playlistKey);
		await delete.ExecuteNonQueryAsync(ct);
	}

	/// <summary>Оставляет составы <see cref="MaxPlaylists"/> самых свежих плейлистов.</summary>
	private static async Task PruneAsync(SqliteConnection connection, CancellationToken ct)
	{
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			DELETE FROM playlistCompositionTracks
			WHERE playlistKey NOT IN (
				SELECT playlistKey FROM playlistCompositions ORDER BY savedAt DESC LIMIT @max);

			DELETE FROM playlistCompositions
			WHERE playlistKey NOT IN (
				SELECT playlistKey FROM playlistCompositions ORDER BY savedAt DESC LIMIT @max);
			""";
		command.Parameters.AddWithValue("@max", MaxPlaylists);

		try
		{
			await command.ExecuteNonQueryAsync(ct);
		}
		catch (Exception exception) when (exception is SqliteException or InvalidOperationException)
		{
			// Чистка не критична: состав всё равно будет заменён при следующем сохранении.
			Log.Debug(exception, "Не удалось почистить старые составы плейлистов");
		}
	}
}
