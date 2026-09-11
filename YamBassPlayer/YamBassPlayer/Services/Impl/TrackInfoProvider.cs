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
	private const string TrackProjection =
		"TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, COALESCE(SourceType, 'yandex'), COALESCE(SourceTrackId, TrackId), LocalFilePath";

	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly IDbWriteLock _writeLock;
	private readonly IMusicSourceRegistry _musicSourceRegistry;

	public TrackInfoProvider(YandexMusicApi api, AuthStorage storage, IDbConnectionFactory connectionFactory, IDbWriteLock writeLock, IMusicSourceRegistry musicSourceRegistry)
	{
		_api = api;
		_storage = storage;
		_connectionFactory = connectionFactory;
		_writeLock = writeLock;
		_musicSourceRegistry = musicSourceRegistry;
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
		string? RemoteCoverUrl,
		string? LocalCoverPath,
		string? GenresJson,
		string? AlbumId,
		string SourceType,
		string SourceTrackId,
		string? LocalFilePath);

	public async Task<IEnumerable<Track>> GetTracksInfoByIds(IEnumerable<string> ids)
	{
		var idsList = ids.ToList();
		if (idsList.Count == 0)
			return [];

		var cachedRows = new Dictionary<string, TrackRow>();

		// Batch query — one round-trip for all IDs
		var paramNames = idsList.Select((_, i) => $"@id{i}").ToList();
		var inClause = string.Join(", ", paramNames);

		using (var connection = _connectionFactory.Create())
		using (var cmd = connection.CreateCommand())
		{
			cmd.CommandText = $@"
				SELECT {TrackProjection}
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
		var remoteMissingIds = missingIds
			.Where(id => !Path.IsPathRooted(id))
			.ToList();

		if (remoteMissingIds.Count > 0)
		{
			YResponse<List<YTrack>>? yResponse = await _api.Track.GetAsync(_storage, remoteMissingIds);
			List<YTrack>? yTracks = yResponse?.Result;

			if (yTracks != null)
			{
				var fetchedTracks = yTracks.Select(y => y.ToTrack()).ToList();

				using (var lockHandle = await _writeLock.AcquireAsync())
				using (var connection = _connectionFactory.Create())
				using (var transaction = connection.BeginTransaction())
				{
					await WriteTracksAsync(connection, transaction, fetchedTracks);
					transaction.Commit();
				}

				foreach (Track track in fetchedTracks)
					cachedRows[track.Id] = ToTrackRow(track);
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
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = @"
			SELECT " + TrackProjection + @"
			FROM Tracks WHERE TrackId = @id";
		cmd.Parameters.AddWithValue("@id", id);

		using var reader = await cmd.ExecuteReaderAsync();
		if (await reader.ReadAsync())
		{
			TrackRow row = ReadTrackRow(reader);
			List<Track> enriched = await EnrichTracksAsync([row]);
			return enriched[0];
		}

		// Un-cached local track: delegate to the local source — its single owner of file parsing.
		if (Path.IsPathRooted(id))
		{
			IMusicSource? localSource = _musicSourceRegistry.Get(SourceIds.Local);
			Track? localTrack = localSource is null ? null : await localSource.GetTrackAsync(id);
			if (localTrack is not null)
				return localTrack;

			throw new InvalidOperationException($"Не удалось получить информацию о треке: {id}");
		}

		Track? track = await TryGetFromApi(id);
		if (track == null)
			throw new InvalidOperationException($"Не удалось получить информацию о треке: {id}");

		return track;
	}

	private async Task<Track?> TryGetFromApi(string id)
	{
		if (Path.IsPathRooted(id))
			return null;

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
		using var lockHandle = await _writeLock.AcquireAsync();
		using var connection = _connectionFactory.Create();
		using var writer = new TrackWriteCommands(connection, null);
		await writer.WriteAsync(track);
	}

	private static async Task WriteTracksAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Track> tracks)
	{
		using var writer = new TrackWriteCommands(connection, transaction);
		foreach (Track track in tracks)
			await writer.WriteAsync(track);
	}

	/// <summary>
	/// Reusable set of prepared commands that writes a track together with its artist links
	/// and album. All commands share one connection (and optional transaction), so a batch of
	/// tracks is persisted without opening a connection per track.
	/// </summary>
	private sealed class TrackWriteCommands : IDisposable
	{
		private readonly SqliteCommand _tracks;
		private readonly SqliteCommand _artists;
		private readonly SqliteCommand _trackArtists;
		private readonly SqliteCommand _albums;

		public TrackWriteCommands(SqliteConnection connection, SqliteTransaction? transaction)
		{
			_tracks = CreateCommand(connection, transaction, @"
				INSERT OR REPLACE INTO Tracks (TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, SourceType, SourceTrackId, LocalFilePath, UpdatedAt)
				VALUES (@TrackId, @artist, @title, @album, @durationMs, @year, @coverUrl, @remoteCoverUrl, @localCoverPath, @genres, @albumId, @sourceType, @sourceTrackId, @localFilePath, @updatedAt)",
				"@TrackId", "@artist", "@title", "@album", "@durationMs", "@year", "@coverUrl", "@remoteCoverUrl", "@localCoverPath", "@genres", "@albumId", "@sourceType", "@sourceTrackId", "@localFilePath", "@updatedAt");

			_artists = CreateCommand(connection, transaction, @"
				INSERT OR REPLACE INTO Artists (Id, Name, CoverUrl, Description, UpdatedAt)
				VALUES (@id, @name, @coverUrl, @description, @updatedAt)",
				"@id", "@name", "@coverUrl", "@description", "@updatedAt");

			_trackArtists = CreateCommand(connection, transaction, @"
				INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId)
				VALUES (@trackId, @artistId)",
				"@trackId", "@artistId");

			_albums = CreateCommand(connection, transaction, @"
				INSERT OR REPLACE INTO Albums (Id, Title, Year, CoverUrl, Genre, TrackCount, UpdatedAt)
				VALUES (@id, @title, @year, @coverUrl, @genre, @trackCount, @updatedAt)",
				"@id", "@title", "@year", "@coverUrl", "@genre", "@trackCount", "@updatedAt");
		}

		public async Task WriteAsync(Track track)
		{
			long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			string? genresJson = track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null;
			string sourceType = track.SourceType ?? SourceIds.Yandex;
			string? remoteCoverUrl = CoverMetadataResolver.ResolveRemoteCoverUrl(sourceType, track.CoverUrl, track.RemoteCoverUrl);
			string? localCoverPath = CoverMetadataResolver.ResolveLocalCoverPath(sourceType, track.CoverUrl, track.LocalCoverPath);
			string? coverUrl = CoverMetadataResolver.ResolveLegacyCoverUrl(sourceType, track.CoverUrl, remoteCoverUrl, localCoverPath);

			Set(_tracks, "@TrackId", track.Id ?? "");
			Set(_tracks, "@artist", track.Artist ?? "");
			Set(_tracks, "@title", track.Title ?? "");
			Set(_tracks, "@album", track.Album ?? "");
			Set(_tracks, "@durationMs", track.DurationMs);
			Set(_tracks, "@year", track.Year);
			Set(_tracks, "@coverUrl", coverUrl);
			Set(_tracks, "@remoteCoverUrl", remoteCoverUrl);
			Set(_tracks, "@localCoverPath", localCoverPath);
			Set(_tracks, "@genres", genresJson);
			Set(_tracks, "@albumId", track.AlbumInfo?.Id);
			Set(_tracks, "@sourceType", sourceType);
			Set(_tracks, "@sourceTrackId", track.SourceTrackId ?? track.Id ?? "");
			Set(_tracks, "@localFilePath", track.LocalFilePath);
			Set(_tracks, "@updatedAt", updatedAt);
			await _tracks.ExecuteNonQueryAsync();

			if (track.Artists != null)
			{
				foreach (Artist artist in track.Artists)
				{
					Set(_artists, "@id", artist.Id);
					Set(_artists, "@name", artist.Name);
					Set(_artists, "@coverUrl", artist.CoverUrl);
					Set(_artists, "@description", artist.Description);
					Set(_artists, "@updatedAt", updatedAt);
					await _artists.ExecuteNonQueryAsync();

					Set(_trackArtists, "@trackId", track.Id ?? "");
					Set(_trackArtists, "@artistId", artist.Id);
					await _trackArtists.ExecuteNonQueryAsync();
				}
			}

			if (track.AlbumInfo is { } albumInfo)
			{
				Set(_albums, "@id", albumInfo.Id);
				Set(_albums, "@title", albumInfo.Title);
				Set(_albums, "@year", albumInfo.Year);
				Set(_albums, "@coverUrl", albumInfo.CoverUrl);
				Set(_albums, "@genre", albumInfo.Genre);
				Set(_albums, "@trackCount", albumInfo.TrackCount);
				Set(_albums, "@updatedAt", updatedAt);
				await _albums.ExecuteNonQueryAsync();
			}
		}

		private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql, params string[] parameterNames)
		{
			var cmd = connection.CreateCommand();
			cmd.Transaction = transaction;
			cmd.CommandText = sql;
			foreach (string name in parameterNames)
				cmd.Parameters.Add(new SqliteParameter(name, DBNull.Value));
			return cmd;
		}

		private static void Set(SqliteCommand cmd, string name, object? value)
			=> cmd.Parameters[name].Value = value ?? DBNull.Value;

		public void Dispose()
		{
			_tracks.Dispose();
			_artists.Dispose();
			_trackArtists.Dispose();
			_albums.Dispose();
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

		using var connection = _connectionFactory.Create();

		// --- 1. Batch-load artists for all track IDs ---
		var artistsByTrackId = new Dictionary<string, List<Artist>>();
		var tidParams = trackIds.Select((_, i) => $"@tid{i}").ToList();

		using (var cmd = connection.CreateCommand())
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

			using var cmd = connection.CreateCommand();
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
				CoverUrl = CoverMetadataResolver.ResolveLegacyCoverUrl(row.SourceType, row.CoverUrl, row.RemoteCoverUrl, row.LocalCoverPath),
				RemoteCoverUrl = CoverMetadataResolver.ResolveRemoteCoverUrl(row.SourceType, row.CoverUrl, row.RemoteCoverUrl),
				LocalCoverPath = CoverMetadataResolver.ResolveLocalCoverPath(row.SourceType, row.CoverUrl, row.LocalCoverPath),
				Genres = genres,
				SourceType = row.SourceType,
				SourceTrackId = row.SourceTrackId,
				LocalFilePath = row.LocalFilePath,
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
		RemoteCoverUrl: reader.IsDBNull(7) ? null : reader.GetString(7),
		LocalCoverPath: reader.IsDBNull(8) ? null : reader.GetString(8),
		GenresJson: reader.IsDBNull(9) ? null : reader.GetString(9),
		AlbumId: reader.IsDBNull(10) ? null : reader.GetString(10),
		SourceType: reader.IsDBNull(11) ? SourceIds.Yandex : reader.GetString(11),
		SourceTrackId: reader.IsDBNull(12) ? reader.GetString(0) : reader.GetString(12),
		LocalFilePath: reader.IsDBNull(13) ? null : reader.GetString(13));

	/// <summary>Converts an in-memory Track to a TrackRow for use in the enrichment pipeline after an API fetch.</summary>
	private static TrackRow ToTrackRow(Track track) => new(
		TrackId: track.Id,
		Artist: track.Artist,
		Title: track.Title,
		Album: track.Album,
		DurationMs: track.DurationMs,
		Year: track.Year,
		CoverUrl: CoverMetadataResolver.ResolveLegacyCoverUrl(track.SourceType, track.CoverUrl, track.RemoteCoverUrl, track.LocalCoverPath),
		RemoteCoverUrl: CoverMetadataResolver.ResolveRemoteCoverUrl(track.SourceType, track.CoverUrl, track.RemoteCoverUrl),
		LocalCoverPath: CoverMetadataResolver.ResolveLocalCoverPath(track.SourceType, track.CoverUrl, track.LocalCoverPath),
		GenresJson: track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null,
		AlbumId: track.AlbumInfo?.Id,
		SourceType: track.SourceType,
		SourceTrackId: track.SourceTrackId,
		LocalFilePath: track.LocalFilePath);

	public async Task<bool> IsTrackCached(string trackId)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = @"
			SELECT TrackId, Artist, Title, Album, COALESCE(SourceType, 'yandex'), COALESCE(SourceTrackId, TrackId), LocalFilePath
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
		{
			string trackId = reader.GetString(0);
			tracks.Add(new Track(reader.GetString(2), reader.GetString(1), reader.GetString(3), trackId)
			{
				SourceType = reader.IsDBNull(4) ? SourceIds.Yandex : reader.GetString(4),
				SourceTrackId = reader.IsDBNull(5) ? trackId : reader.GetString(5),
				LocalFilePath = reader.IsDBNull(6) ? null : reader.GetString(6),
			});
		}

		return tracks;
	}
}
