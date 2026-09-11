using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Фасад над кэшем треков: чтение/запись строк (<see cref="TrackRowMapper"/>,
/// <see cref="TrackCacheWriter"/>, <see cref="TrackEnricher"/>) и дозагрузка из API.
/// </summary>
public class TrackInfoProvider : ITrackInfoProvider
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly IMusicSourceRegistry _musicSourceRegistry;
	private readonly TrackCacheWriter _cacheWriter;
	private readonly TrackEnricher _enricher;

	public TrackInfoProvider(YandexMusicApi api, AuthStorage storage, IDbConnectionFactory connectionFactory, IDbWriteLock writeLock, IMusicSourceRegistry musicSourceRegistry)
	{
		_api = api;
		_storage = storage;
		_connectionFactory = connectionFactory;
		_musicSourceRegistry = musicSourceRegistry;
		_cacheWriter = new TrackCacheWriter(connectionFactory, writeLock);
		_enricher = new TrackEnricher(connectionFactory);
	}

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
				SELECT {TrackRowMapper.TrackProjection}
				FROM Tracks WHERE TrackId IN ({inClause})";
			for (int i = 0; i < idsList.Count; i++)
				cmd.Parameters.AddWithValue(paramNames[i], idsList[i]);

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				TrackRow row = TrackRowMapper.ReadTrackRow(reader);
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
				await _cacheWriter.SaveBatchAsync(fetchedTracks);

				foreach (Track track in fetchedTracks)
					cachedRows[track.Id] = TrackRowMapper.ToTrackRow(track);
			}
		}

		// Batch-enrich all rows with Artists and AlbumInfo, then restore original order
		var enrichedById = (await _enricher.EnrichAsync(cachedRows.Values.ToList()))
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
			SELECT " + TrackRowMapper.TrackProjection + @"
			FROM Tracks WHERE TrackId = @id";
		cmd.Parameters.AddWithValue("@id", id);

		using var reader = await cmd.ExecuteReaderAsync();
		if (await reader.ReadAsync())
		{
			TrackRow row = TrackRowMapper.ReadTrackRow(reader);
			List<Track> enriched = await _enricher.EnrichAsync([row]);
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

	public Task SaveAsync(Track track) => _cacheWriter.SaveAsync(track);

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
