using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Локальное избранное в таблице <c>favoriteLocalTracks</c>.
/// Хранится ключ <c>(sourceId, trackId)</c>, поэтому в избранное можно класть
/// треки любого источника; лайки Яндекс.Музыки к этой таблице отношения не имеют.
/// </summary>
public sealed class LocalFavoriteService : ILocalFavoriteService
{
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly IDbWriteLock _writeLock;
	private readonly List<TrackKey> _favorites = [];

	public LocalFavoriteService(IDbConnectionFactory connectionFactory, IDbWriteLock writeLock)
	{
		_connectionFactory = connectionFactory;
		_writeLock = writeLock;

		Load();
	}

	public event Action? Changed;

	public bool IsTrackFavorite(TrackKey key)
	{
		lock (_favorites)
			return _favorites.Contains(key);
	}

	public async Task AddToFavorites(TrackKey key)
	{
		if (IsTrackFavorite(key))
			return;

		using (await _writeLock.AcquireAsync())
		using (SqliteConnection connection = _connectionFactory.Create())
		using (SqliteCommand command = connection.CreateCommand())
		{
			command.CommandText =
				"""
				INSERT OR REPLACE INTO favoriteLocalTracks (sourceId, trackId, addedAt)
				VALUES (@sourceId, @trackId, @addedAt);
				""";
			command.Parameters.AddWithValue("@sourceId", key.SourceId.Value);
			command.Parameters.AddWithValue("@trackId", key.TrackId);
			command.Parameters.AddWithValue("@addedAt", DateTime.UtcNow.ToString("O"));
			await command.ExecuteNonQueryAsync();
		}

		lock (_favorites)
		{
			if (!_favorites.Contains(key))
				_favorites.Insert(0, key);
		}

		Changed?.Invoke();
	}

	public async Task RemoveFromFavorites(TrackKey key)
	{
		if (!IsTrackFavorite(key))
			return;

		using (await _writeLock.AcquireAsync())
		using (SqliteConnection connection = _connectionFactory.Create())
		using (SqliteCommand command = connection.CreateCommand())
		{
			command.CommandText =
				"DELETE FROM favoriteLocalTracks WHERE sourceId = @sourceId AND trackId = @trackId;";
			command.Parameters.AddWithValue("@sourceId", key.SourceId.Value);
			command.Parameters.AddWithValue("@trackId", key.TrackId);
			await command.ExecuteNonQueryAsync();
		}

		lock (_favorites)
			_favorites.Remove(key);

		Changed?.Invoke();
	}

	public Task<IReadOnlyList<TrackKey>> GetAllFavoriteKeys()
	{
		lock (_favorites)
			return Task.FromResult<IReadOnlyList<TrackKey>>(_favorites.ToList());
	}

	private void Load()
	{
		using SqliteConnection connection = _connectionFactory.Create();
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"SELECT sourceId, trackId FROM favoriteLocalTracks ORDER BY addedAt DESC;";

		using SqliteDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			string sourceId = reader.GetString(0);
			string trackId = reader.GetString(1);

			if (!string.IsNullOrWhiteSpace(sourceId) && !string.IsNullOrWhiteSpace(trackId))
				_favorites.Add(new TrackKey(new SourceId(sourceId), trackId));
		}
	}
}
