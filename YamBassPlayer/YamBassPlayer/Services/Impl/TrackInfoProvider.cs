using Microsoft.Data.Sqlite;
using System.Text.Json;
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

	// Raw DB row before artist/album enrichment
	private sealed record TrackRow(
		string TrackId,
		string Artist,
		string Title,
		string Album,
		long? DurationMs,
		int? Year,
		string? CoverUrl,
		string? GenresJson,
		string? AlbumId,
		string SourceType);

	public async Task<IEnumerable<Track>> GetTracksInfoByIds(IEnumerable<string> ids)
	{
		var idsList = ids.ToList();
		if (idsList.Count == 0)
			return [];

		var cachedRows = new Dictionary<string, TrackRow>();

		// Batch query — one round-trip for all IDs
		var paramNames = idsList.Select((_, i) => $"@id{i}").ToList();
		var inClause = string.Join(", ", paramNames);

		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = $@"
				SELECT TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, Genres, AlbumId, COALESCE(SourceType, 'yandex')
				FROM Tracks WHERE TrackId IN ({inClause})";
			for (int i = 0; i < idsList.Count; i++)
				cmd.Parameters.AddWithValue(paramNames[i], idsList[i]);

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				TrackRow row = ReadTrackRow(reader);
				cachedRows[row.TrackId] = row;
			}
		}

		var missingIds = idsList.Where(id => !cachedRows.ContainsKey(id)).ToList();

		if (missingIds.Count > 0)
		{
			YResponse<List<YTrack>>? yResponse = await _api.Track.GetAsync(_storage, missingIds);
			List<YTrack>? yTracks = yResponse?.Result;

			if (yTracks != null)
			{
				foreach (YTrack yTrack in yTracks)
				{
					Track track = yTrack.ToTrack();
					await SaveAsync(track);
					cachedRows[track.Id] = ToTrackRow(track);
				}
			}
		}

		// Batch-enrich all rows with Artists and AlbumInfo, then restore original order
		var enrichedById = (await EnrichTracksAsync(cachedRows.Values.ToList()))
			.ToDictionary(t => t.Id);

		var tracksResult = new List<Track>();
		foreach (string id in idsList)
		{
			if (enrichedById.TryGetValue(id, out Track? track))
				tracksResult.Add(track);
		}

		return tracksResult;
	}

	public async Task<Track> GetTrackInfoById(string id)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = @"
			SELECT TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, Genres, AlbumId, COALESCE(SourceType, 'yandex')
			FROM Tracks WHERE TrackId = @id";
		cmd.Parameters.AddWithValue("@id", id);

		using var reader = await cmd.ExecuteReaderAsync();
		if (await reader.ReadAsync())
		{
			TrackRow row = ReadTrackRow(reader);
			List<Track> enriched = await EnrichTracksAsync([row]);
			return enriched[0];
		}

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
		long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		string? genresJson = track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null;

		// Save to Tracks table with all enriched columns
		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = @"
				INSERT OR REPLACE INTO Tracks (TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, Genres, AlbumId, SourceType, UpdatedAt)
				VALUES (@TrackId, @artist, @title, @album, @durationMs, @year, @coverUrl, @genres, @albumId, @sourceType, @updatedAt)";
			cmd.Parameters.AddWithValue("@TrackId", track.Id ?? "");
			cmd.Parameters.AddWithValue("@artist", track.Artist ?? "");
			cmd.Parameters.AddWithValue("@title", track.Title ?? "");
			cmd.Parameters.AddWithValue("@album", track.Album ?? "");
			cmd.Parameters.AddWithValue("@durationMs", (object?)track.DurationMs ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@year", (object?)track.Year ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@coverUrl", (object?)track.CoverUrl ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@genres", (object?)genresJson ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@albumId", (object?)track.AlbumInfo?.Id ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@sourceType", track.SourceType ?? "yandex");
			cmd.Parameters.AddWithValue("@updatedAt", updatedAt);
			await cmd.ExecuteNonQueryAsync();
		}

		// Save artists and track-artist links
		if (track.Artists != null)
		{
			foreach (Artist artist in track.Artists)
			{
				using (var artistCmd = _connection.CreateCommand())
				{
					artistCmd.CommandText = @"
						INSERT OR REPLACE INTO Artists (Id, Name, CoverUrl, Description, UpdatedAt)
						VALUES (@id, @name, @coverUrl, @description, @updatedAt)";
					artistCmd.Parameters.AddWithValue("@id", artist.Id);
					artistCmd.Parameters.AddWithValue("@name", artist.Name);
					artistCmd.Parameters.AddWithValue("@coverUrl", (object?)artist.CoverUrl ?? DBNull.Value);
					artistCmd.Parameters.AddWithValue("@description", (object?)artist.Description ?? DBNull.Value);
					artistCmd.Parameters.AddWithValue("@updatedAt", updatedAt);
					await artistCmd.ExecuteNonQueryAsync();
				}

				using (var linkCmd = _connection.CreateCommand())
				{
					linkCmd.CommandText = @"
						INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId)
						VALUES (@trackId, @artistId)";
					linkCmd.Parameters.AddWithValue("@trackId", track.Id ?? "");
					linkCmd.Parameters.AddWithValue("@artistId", artist.Id);
					await linkCmd.ExecuteNonQueryAsync();
				}
			}
		}

		// Save album
		if (track.AlbumInfo is { } albumInfo)
		{
			using var albumCmd = _connection.CreateCommand();
			albumCmd.CommandText = @"
				INSERT OR REPLACE INTO Albums (Id, Title, Year, CoverUrl, Genre, TrackCount, UpdatedAt)
				VALUES (@id, @title, @year, @coverUrl, @genre, @trackCount, @updatedAt)";
			albumCmd.Parameters.AddWithValue("@id", albumInfo.Id);
			albumCmd.Parameters.AddWithValue("@title", albumInfo.Title);
			albumCmd.Parameters.AddWithValue("@year", (object?)albumInfo.Year ?? DBNull.Value);
			albumCmd.Parameters.AddWithValue("@coverUrl", (object?)albumInfo.CoverUrl ?? DBNull.Value);
			albumCmd.Parameters.AddWithValue("@genre", (object?)albumInfo.Genre ?? DBNull.Value);
			albumCmd.Parameters.AddWithValue("@trackCount", (object?)albumInfo.TrackCount ?? DBNull.Value);
			albumCmd.Parameters.AddWithValue("@updatedAt", updatedAt);
			await albumCmd.ExecuteNonQueryAsync();
		}
	}

	/// <summary>
	/// Batch-enriches raw track rows with Artists and AlbumInfo using two additional DB round-trips.
	/// </summary>
	private async Task<List<Track>> EnrichTracksAsync(IReadOnlyList<TrackRow> rows)
	{
		if (rows.Count == 0)
			return [];

		var trackIds = rows.Select(r => r.TrackId).Distinct().ToList();

		// --- 1. Batch-load artists for all track IDs ---
		var artistsByTrackId = new Dictionary<string, List<Artist>>();
		var tidParams = trackIds.Select((_, i) => $"@tid{i}").ToList();

		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = $@"
				SELECT ta.TrackId, a.Id, a.Name, a.CoverUrl, a.Description
				FROM TrackArtists ta
				JOIN Artists a ON ta.ArtistId = a.Id
				WHERE ta.TrackId IN ({string.Join(", ", tidParams)})";
			for (int i = 0; i < trackIds.Count; i++)
				cmd.Parameters.AddWithValue(tidParams[i], trackIds[i]);

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				string trackId = reader.GetString(0);
				var artist = new Artist(reader.GetString(1), reader.GetString(2))
				{
					CoverUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
					Description = reader.IsDBNull(4) ? null : reader.GetString(4),
				};

				if (!artistsByTrackId.TryGetValue(trackId, out List<Artist>? list))
					artistsByTrackId[trackId] = list = [];
				list.Add(artist);
			}
		}

		// --- 2. Batch-load albums for all referenced album IDs ---
		var albumIds = rows
			.Where(r => r.AlbumId is not null)
			.Select(r => r.AlbumId!)
			.Distinct()
			.ToList();

		var albumsById = new Dictionary<string, Album>();

		if (albumIds.Count > 0)
		{
			var aidParams = albumIds.Select((_, i) => $"@aid{i}").ToList();

			using var cmd = _connection.CreateCommand();
			cmd.CommandText = $@"
				SELECT Id, Title, Year, CoverUrl, Genre, TrackCount
				FROM Albums
				WHERE Id IN ({string.Join(", ", aidParams)})";
			for (int i = 0; i < albumIds.Count; i++)
				cmd.Parameters.AddWithValue(aidParams[i], albumIds[i]);

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				var album = new Album(reader.GetString(0), reader.GetString(1))
				{
					Year = reader.IsDBNull(2) ? null : reader.GetInt32(2),
					CoverUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
					Genre = reader.IsDBNull(4) ? null : reader.GetString(4),
					TrackCount = reader.IsDBNull(5) ? null : reader.GetInt32(5),
				};
				albumsById[album.Id] = album;
			}
		}

		// --- 3. Assemble enriched Track objects ---
		var result = new List<Track>(rows.Count);
		foreach (TrackRow row in rows)
		{
			IReadOnlyList<Artist>? artists = artistsByTrackId.TryGetValue(row.TrackId, out List<Artist>? artistList)
				? artistList
				: null;

			Album? albumInfo = row.AlbumId is not null && albumsById.TryGetValue(row.AlbumId, out Album? album)
				? album
				: null;

			IReadOnlyList<string>? genres = null;
			if (row.GenresJson is not null)
			{
				try { genres = JsonSerializer.Deserialize<List<string>>(row.GenresJson); }
				catch { /* ignore malformed JSON — treat as no genres */ }
			}

			result.Add(new Track(row.Title, row.Artist, row.Album, row.TrackId)
			{
				DurationMs = row.DurationMs,
				Year = row.Year,
				CoverUrl = row.CoverUrl,
				Genres = genres,
				SourceType = row.SourceType,
				Artists = artists,
				AlbumInfo = albumInfo,
			});
		}

		return result;
	}

	private static TrackRow ReadTrackRow(SqliteDataReader reader) => new(
		TrackId: reader.GetString(0),
		Artist: reader.IsDBNull(1) ? "" : reader.GetString(1),
		Title: reader.IsDBNull(2) ? "" : reader.GetString(2),
		Album: reader.IsDBNull(3) ? "" : reader.GetString(3),
		DurationMs: reader.IsDBNull(4) ? null : reader.GetInt64(4),
		Year: reader.IsDBNull(5) ? null : reader.GetInt32(5),
		CoverUrl: reader.IsDBNull(6) ? null : reader.GetString(6),
		GenresJson: reader.IsDBNull(7) ? null : reader.GetString(7),
		AlbumId: reader.IsDBNull(8) ? null : reader.GetString(8),
		SourceType: reader.IsDBNull(9) ? "yandex" : reader.GetString(9));

	/// <summary>Converts an in-memory Track to a TrackRow for use in the enrichment pipeline after an API fetch.</summary>
	private static TrackRow ToTrackRow(Track track) => new(
		TrackId: track.Id,
		Artist: track.Artist,
		Title: track.Title,
		Album: track.Album,
		DurationMs: track.DurationMs,
		Year: track.Year,
		CoverUrl: track.CoverUrl,
		GenresJson: track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null,
		AlbumId: track.AlbumInfo?.Id,
		SourceType: track.SourceType);

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
