using Microsoft.Data.Sqlite;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public class TrackInfoProvider : ITrackInfoProvider
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly SqliteConnection _connection;

	public TrackInfoProvider(YandexMusicApi api, AuthStorage storage, SqliteConnection connection)
	{
		_api = api;
		_storage = storage;
		_connection = connection;

		using SqliteCommand cmd = connection.CreateCommand();
		cmd.CommandText = @"
			CREATE TABLE IF NOT EXISTS Tracks (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				TrackId TEXT UNIQUE,
				Artist TEXT,
				Title TEXT,
				Album TEXT,
				UpdatedAt INTEGER
			);";
		cmd.ExecuteNonQuery();
	}

	public async Task<IEnumerable<Track>> GetTracksInfoByIds(IEnumerable<string> ids)
	{
		var idsList = ids.ToList();
		if (idsList.Count == 0)
			return [];

		var tracksResult = new List<Track>();
		var cachedById = new Dictionary<string, Track>();

		// Batch query — one round-trip for all IDs
		var paramNames = idsList.Select((_, i) => $"@id{i}").ToList();
		var inClause = string.Join(", ", paramNames);

		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = $"SELECT TrackId, Artist, Title, Album FROM Tracks WHERE TrackId IN ({inClause})";
			for (int i = 0; i < idsList.Count; i++)
				cmd.Parameters.AddWithValue(paramNames[i], idsList[i]);

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				var t = new Track(reader.GetString(2), reader.GetString(1), reader.GetString(3), reader.GetString(0));
				cachedById[t.Id] = t;
			}
		}

		var missingIds = idsList.Where(id => !cachedById.ContainsKey(id)).ToList();

		if (missingIds.Count > 0)
		{
			YResponse<List<YTrack>>? yResponse = await _api.Track.GetAsync(_storage, missingIds);
			List<YTrack>? yTracks = yResponse?.Result;

			if (yTracks != null)
			{
				foreach (YTrack yTrack in yTracks)
				{
					string artists = yTrack.Artists != null
						? string.Join(", ", yTrack.Artists.Select(a => a.Name))
						: "Неизвестный исполнитель";

					string album = yTrack.Albums != null && yTrack.Albums.Count > 0
						? yTrack.Albums.First().Title
						: "";

					var track = new Track(yTrack.Title, artists, album, yTrack.Id);
					await SaveAsync(track);
					cachedById[track.Id] = track;
				}
			}
		}

		// Preserve original order
		foreach (string id in idsList)
		{
			if (cachedById.TryGetValue(id, out var track))
				tracksResult.Add(track);
		}

		return tracksResult;
	}

	public async Task<Track> GetTrackInfoById(string id)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "SELECT TrackId, Artist, Title, Album FROM Tracks WHERE TrackId = @id";
		cmd.Parameters.AddWithValue("@id", id);

		using var reader = await cmd.ExecuteReaderAsync();
		if (await reader.ReadAsync())
			return new Track(reader.GetString(2), reader.GetString(1), reader.GetString(3), reader.GetString(0));

		Track? track = await TryGetFromApi(id);
		if (track == null)
			throw new InvalidOperationException($"Не удалось получить информацию о треке: {id}");

		return track;
	}

	private async Task<Track?> TryGetFromApi(string id)
	{
		YResponse<List<YTrack>>? trackResponse = await _api.Track.GetAsync(_storage, id);
		YTrack? track = trackResponse?.Result?.FirstOrDefault();

		if (track == null)
			return null;

		Track trackVm = track.ToTrack();
		await SaveAsync(trackVm);
		return trackVm;
	}

	public async Task SaveAsync(Track track)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = @"
			INSERT OR REPLACE INTO Tracks (TrackId, Artist, Title, Album, UpdatedAt)
			VALUES (@TrackId, @artist, @title, @album, @updatedAt)";
		cmd.Parameters.AddWithValue("@TrackId", track.Id ?? "");
		cmd.Parameters.AddWithValue("@artist", track.Artist ?? "");
		cmd.Parameters.AddWithValue("@title", track.Title ?? "");
		cmd.Parameters.AddWithValue("@album", track.Album ?? "");
		cmd.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

		await cmd.ExecuteNonQueryAsync();
	}

	public async Task<bool> IsTrackCached(string trackId)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "SELECT 1 FROM Tracks WHERE TrackId = @id LIMIT 1";
		cmd.Parameters.AddWithValue("@id", trackId);

		var result = await cmd.ExecuteScalarAsync();
		return result != null;
	}

	// Returns the count of leading consecutive cached tracks (stops at first miss).
	public async Task<int> CountCachedTracks(IEnumerable<string> trackIds)
	{
		var idsList = trackIds.ToList();
		if (idsList.Count == 0)
			return 0;

		var paramNames = idsList.Select((_, i) => $"@id{i}").ToList();
		var inClause = string.Join(", ", paramNames);

		using var cmd = _connection.CreateCommand();
		cmd.CommandText = $"SELECT TrackId FROM Tracks WHERE TrackId IN ({inClause})";
		for (int i = 0; i < idsList.Count; i++)
			cmd.Parameters.AddWithValue(paramNames[i], idsList[i]);

		var cachedSet = new HashSet<string>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			cachedSet.Add(reader.GetString(0));

		int count = 0;
		foreach (var id in idsList)
		{
			if (!cachedSet.Contains(id)) break;
			count++;
		}
		return count;
	}

	public async Task<IReadOnlyList<(string artistName, int trackCount)>> GetArtistsWithTrackCountAsync()
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = @"
			SELECT CASE WHEN Artist = '' OR Artist IS NULL THEN 'Неизвестный исполнитель' ELSE Artist END AS ArtistName,
			       COUNT(*) AS TrackCount
			FROM Tracks
			GROUP BY ArtistName
			ORDER BY ArtistName ASC";

		var result = new List<(string artistName, int trackCount)>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			result.Add((reader.GetString(0), reader.GetInt32(1)));

		return result;
	}

	public async Task<List<string>> GetTrackIdsByArtistAsync(string artistName)
	{
		using var cmd = _connection.CreateCommand();
		if (artistName == "Неизвестный исполнитель")
		{
			cmd.CommandText = "SELECT TrackId FROM Tracks WHERE Artist = '' OR Artist IS NULL ORDER BY Album, Title";
		}
		else
		{
			cmd.CommandText = "SELECT TrackId FROM Tracks WHERE Artist = @artist ORDER BY Album, Title";
			cmd.Parameters.AddWithValue("@artist", artistName);
		}

		var trackIds = new List<string>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			trackIds.Add(reader.GetString(0));

		return trackIds;
	}

	public async Task<IEnumerable<Track>> SearchTracks(string searchQuery, int maxResults = 50)
	{
		if (string.IsNullOrWhiteSpace(searchQuery))
			return [];

		using var cmd = _connection.CreateCommand();
		cmd.CommandText = @"
			SELECT TrackId, Artist, Title, Album 
			FROM Tracks 
			WHERE Title LIKE @query 
			   OR Artist LIKE @query 
			   OR Album LIKE @query
			LIMIT @maxResults";

		string likeQuery = $"%{searchQuery}%";
		cmd.Parameters.AddWithValue("@query", likeQuery);
		cmd.Parameters.AddWithValue("@maxResults", maxResults);

		var tracks = new List<Track>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			tracks.Add(new Track(reader.GetString(2), reader.GetString(1), reader.GetString(3), reader.GetString(0)));

		return tracks;
	}
}
