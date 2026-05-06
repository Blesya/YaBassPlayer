using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class LocalFavoriteService : ILocalFavoriteService
{
	private readonly SqliteConnection _connection;
	private readonly HashSet<string> _favoriteTrackIds = new();
	public string SourceId => SourceIds.Local;

	public event Action<string>? OnFavoriteAdded;
	public event Action<string>? OnFavoriteRemoved;

	public LocalFavoriteService(SqliteConnection connection)
	{
		_connection = connection;
		EnsureSchema();
		Task.Run(LoadFavorites);
	}

	private void EnsureSchema()
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText =
			"""
			CREATE TABLE IF NOT EXISTS favoriteLocalTracks (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				trackId TEXT UNIQUE NOT NULL,
				addedAt INTEGER NOT NULL
			);
			""";
		cmd.ExecuteNonQuery();
	}

	private void LoadFavorites()
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "SELECT trackId FROM favoriteLocalTracks;";
		
		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			lock (_favoriteTrackIds)
				_favoriteTrackIds.Add(reader.GetString(0));
		}
	}

	public bool IsTrackFavorite(string trackId)
	{
		lock (_favoriteTrackIds)
			return _favoriteTrackIds.Contains(trackId);
	}

	public async Task AddToFavorites(string trackId)
	{
		lock (_favoriteTrackIds)
		{
			if (_favoriteTrackIds.Contains(trackId))
				return;
			_favoriteTrackIds.Add(trackId);
		}

		await Task.Run(() =>
		{
			using var cmd = _connection.CreateCommand();
			cmd.CommandText =
				"""
				INSERT OR IGNORE INTO favoriteLocalTracks (trackId, addedAt)
				VALUES ($trackId, $addedAt);
				""";
			
			cmd.Parameters.AddWithValue("$trackId", trackId);
			cmd.Parameters.AddWithValue("$addedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			
			cmd.ExecuteNonQuery();
		});

		OnFavoriteAdded?.Invoke(trackId);
	}

	public async Task RemoveFromFavorites(string trackId)
	{
		lock (_favoriteTrackIds)
		{
			if (!_favoriteTrackIds.Contains(trackId))
				return;
			_favoriteTrackIds.Remove(trackId);
		}

		await Task.Run(() =>
		{
			using var cmd = _connection.CreateCommand();
			cmd.CommandText = "DELETE FROM favoriteLocalTracks WHERE trackId = $trackId;";
			
			cmd.Parameters.AddWithValue("$trackId", trackId);
			
			cmd.ExecuteNonQuery();
		});

		OnFavoriteRemoved?.Invoke(trackId);
	}

	public async Task<List<string>> GetAllFavoriteTrackIds()
	{
		return await Task.Run(() =>
		{
			var result = new List<string>();
			
			using var cmd = _connection.CreateCommand();
			cmd.CommandText = "SELECT trackId FROM favoriteLocalTracks ORDER BY addedAt DESC;";
			
			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				result.Add(reader.GetString(0));
			}
			
			return result;
		});
	}
}
