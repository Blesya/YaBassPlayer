using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class LocalFavoriteService : ILocalFavoriteService
{
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly HashSet<string> _favoriteTrackIds = new();
	public string SourceId => SourceIds.Local;

	public event Action<string>? OnFavoriteAdded;
	public event Action<string>? OnFavoriteRemoved;

	public LocalFavoriteService(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
		// Load synchronously on a dedicated connection so the in-memory index is ready before
		// the service is handed out (no fire-and-forget work racing with shutdown/tests).
		LoadFavorites();
	}

	private void LoadFavorites()
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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

	public Task AddToFavorites(string trackId)
	{
		lock (_favoriteTrackIds)
		{
			if (_favoriteTrackIds.Contains(trackId))
				return Task.CompletedTask;
			_favoriteTrackIds.Add(trackId);
		}

		using (var connection = _connectionFactory.Create())
		using (var cmd = connection.CreateCommand())
		{
			cmd.CommandText =
				"""
				INSERT OR IGNORE INTO favoriteLocalTracks (trackId, addedAt)
				VALUES ($trackId, $addedAt);
				""";

			cmd.Parameters.AddWithValue("$trackId", trackId);
			cmd.Parameters.AddWithValue("$addedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

			cmd.ExecuteNonQuery();
		}

		OnFavoriteAdded?.Invoke(trackId);
		return Task.CompletedTask;
	}

	public Task RemoveFromFavorites(string trackId)
	{
		lock (_favoriteTrackIds)
		{
			if (!_favoriteTrackIds.Contains(trackId))
				return Task.CompletedTask;
			_favoriteTrackIds.Remove(trackId);
		}

		using (var connection = _connectionFactory.Create())
		using (var cmd = connection.CreateCommand())
		{
			cmd.CommandText = "DELETE FROM favoriteLocalTracks WHERE trackId = $trackId;";

			cmd.Parameters.AddWithValue("$trackId", trackId);

			cmd.ExecuteNonQuery();
		}

		OnFavoriteRemoved?.Invoke(trackId);
		return Task.CompletedTask;
	}

	public Task<List<string>> GetAllFavoriteTrackIds()
	{
		var result = new List<string>();

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT trackId FROM favoriteLocalTracks ORDER BY addedAt DESC;";

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			result.Add(reader.GetString(0));
		}

		return Task.FromResult(result);
	}
}
