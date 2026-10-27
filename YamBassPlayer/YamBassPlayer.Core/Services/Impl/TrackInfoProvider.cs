using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Фасад над кэшем треков: чтение/запись строк (<see cref="TrackRowMapper"/>,
/// <see cref="TrackCacheWriter"/>, <see cref="TrackEnricher"/>) и дозагрузка из источника.
/// </summary>
public class TrackInfoProvider : ITrackInfoProvider
{
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly IMusicSourceRegistry _musicSourceRegistry;
	private readonly TrackCacheWriter _cacheWriter;
	private readonly TrackEnricher _enricher;

	public TrackInfoProvider(IDbConnectionFactory connectionFactory, IDbWriteLock writeLock, IMusicSourceRegistry musicSourceRegistry)
	{
		_connectionFactory = connectionFactory;
		_musicSourceRegistry = musicSourceRegistry;
		_cacheWriter = new TrackCacheWriter(connectionFactory, writeLock);
		_enricher = new TrackEnricher(connectionFactory);
	}

	public async Task<IEnumerable<Track>> GetTracksInfoByIds(IEnumerable<TrackKey> keys)
	{
		var keysList = keys.Distinct().ToList();
		if (keysList.Count == 0)
			return [];

		Dictionary<TrackKey, TrackRow> cachedRows = await ReadCachedRowsAsync(keysList);

		var missingKeys = keysList.Where(key => !cachedRows.ContainsKey(key)).ToList();
		foreach (var group in missingKeys.GroupBy(key => key.SourceId))
		{
			IMusicSource? source = _musicSourceRegistry.Get(group.Key);
			if (source is null)
				continue;

			var fetchedTracks = (await source.GetTracksByIdsAsync(group.Select(key => key.TrackId))).ToList();
			if (fetchedTracks.Count == 0)
				continue;

			await _cacheWriter.SaveBatchAsync(fetchedTracks);

			foreach (Track track in fetchedTracks)
				cachedRows[track.Key] = TrackRowMapper.ToTrackRow(track);
		}

		return await EnrichOrderedAsync(keysList, cachedRows);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<Track>> GetCachedTracksByIds(IEnumerable<TrackKey> keys)
	{
		var keysList = keys.Distinct().ToList();
		if (keysList.Count == 0)
			return [];

		Dictionary<TrackKey, TrackRow> cachedRows = await ReadCachedRowsAsync(keysList);
		return await EnrichOrderedAsync(keysList, cachedRows);
	}

	/// <summary>Один пакетный запрос к БД: строки треков по ключам, без обращения к источнику.</summary>
	private async Task<Dictionary<TrackKey, TrackRow>> ReadCachedRowsAsync(IReadOnlyList<TrackKey> keysList)
	{
		var cachedRows = new Dictionary<TrackKey, TrackRow>();

		var valueRows = new List<string>();
		var parameters = new List<(string Name, object Value)>();
		for (int i = 0; i < keysList.Count; i++)
		{
			string sourceName = $"@source{i}";
			string trackName = $"@track{i}";
			valueRows.Add($"({sourceName}, {trackName})");
			parameters.Add((sourceName, keysList[i].SourceId.Value));
			parameters.Add((trackName, keysList[i].TrackId));
		}

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = $@"
			SELECT {TrackRowMapper.TrackProjection}
			FROM Tracks WHERE (SourceType, TrackId) IN (VALUES {string.Join(", ", valueRows)})";
		foreach (var (name, value) in parameters)
			cmd.Parameters.AddWithValue(name, value);

		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			TrackRow row = TrackRowMapper.ReadTrackRow(reader);
			cachedRows[new TrackKey(row.SourceId, row.TrackId)] = row;
		}

		return cachedRows;
	}

	/// <summary>Обогащает строки исполнителями и альбомами и восстанавливает исходный порядок.</summary>
	private async Task<IReadOnlyList<Track>> EnrichOrderedAsync(
		IReadOnlyList<TrackKey> keysList,
		Dictionary<TrackKey, TrackRow> cachedRows)
	{
		if (cachedRows.Count == 0)
			return [];

		var enrichedByKey = (await _enricher.EnrichAsync(cachedRows.Values.ToList()))
			.ToDictionary(t => t.Key);

		var tracksResult = new List<Track>();
		foreach (TrackKey key in keysList)
		{
			if (enrichedByKey.TryGetValue(key, out Track? track))
				tracksResult.Add(track);
		}

		return tracksResult;
	}

	public async Task<Track> GetTrackInfoById(TrackKey key)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = @"
			SELECT " + TrackRowMapper.TrackProjection + @"
			FROM Tracks WHERE SourceType = @sourceType AND TrackId = @id";
		cmd.Parameters.AddWithValue("@sourceType", key.SourceId.Value);
		cmd.Parameters.AddWithValue("@id", key.TrackId);

		using var reader = await cmd.ExecuteReaderAsync();
		if (await reader.ReadAsync())
		{
			TrackRow row = TrackRowMapper.ReadTrackRow(reader);
			List<Track> enriched = await _enricher.EnrichAsync([row]);
			return enriched[0];
		}

		// Un-cached track: delegate to its source — the single owner of parsing/fetching.
		IMusicSource? source = _musicSourceRegistry.Get(key.SourceId);
		Track? track = source is null ? null : await source.GetTrackAsync(key.TrackId);
		if (track is null)
			throw new InvalidOperationException($"Не удалось получить информацию о треке: {key}");

		await SaveAsync(track);
		return track;
	}

	public Task SaveAsync(Track track) => _cacheWriter.SaveAsync(track);

	public async Task<bool> IsTrackCached(TrackKey key)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT 1 FROM Tracks WHERE SourceType = @sourceType AND TrackId = @id LIMIT 1";
		cmd.Parameters.AddWithValue("@sourceType", key.SourceId.Value);
		cmd.Parameters.AddWithValue("@id", key.TrackId);

		var result = await cmd.ExecuteScalarAsync();
		return result != null;
	}

	// Returns the count of leading consecutive cached tracks (stops at first miss).
	public async Task<int> CountCachedTracks(IEnumerable<TrackKey> keys)
	{
		var keysList = keys.ToList();
		if (keysList.Count == 0)
			return 0;

		var valueRows = new List<string>();
		var parameters = new List<(string Name, object Value)>();
		for (int i = 0; i < keysList.Count; i++)
		{
			string sourceName = $"@source{i}";
			string trackName = $"@track{i}";
			valueRows.Add($"({sourceName}, {trackName})");
			parameters.Add((sourceName, keysList[i].SourceId.Value));
			parameters.Add((trackName, keysList[i].TrackId));
		}

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = $"SELECT SourceType, TrackId FROM Tracks WHERE (SourceType, TrackId) IN (VALUES {string.Join(", ", valueRows)})";
		foreach (var (name, value) in parameters)
			cmd.Parameters.AddWithValue(name, value);

		var cachedSet = new HashSet<TrackKey>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			cachedSet.Add(new TrackKey(new SourceId(reader.GetString(0)), reader.GetString(1)));

		int count = 0;
		foreach (var key in keysList)
		{
			if (!cachedSet.Contains(key)) break;
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

	public async Task<IReadOnlyList<TrackKey>> GetTrackIdsByArtistAsync(string artistName)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		if (artistName == "Неизвестный исполнитель")
		{
			cmd.CommandText = "SELECT SourceType, TrackId FROM Tracks WHERE Artist = '' OR Artist IS NULL ORDER BY Album, Title";
		}
		else
		{
			cmd.CommandText = "SELECT SourceType, TrackId FROM Tracks WHERE Artist = @artist ORDER BY Album, Title";
			cmd.Parameters.AddWithValue("@artist", artistName);
		}

		var trackKeys = new List<TrackKey>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			trackKeys.Add(new TrackKey(new SourceId(reader.GetString(0)), reader.GetString(1)));

		return trackKeys;
	}

	public async Task<IEnumerable<Track>> SearchTracks(string searchQuery, int maxResults = 50)
	{
		if (string.IsNullOrWhiteSpace(searchQuery))
			return [];

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = @"
			SELECT TrackId, Artist, Title, Album, SourceType, LocalFilePath
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
				SourceId = new SourceId(reader.GetString(4)),
				LocalFilePath = reader.IsDBNull(5) ? null : reader.GetString(5),
			});
		}

		return tracks;
	}
}
