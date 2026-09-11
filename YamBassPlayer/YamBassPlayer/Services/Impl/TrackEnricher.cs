using System.Text.Json;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Обогащает сырые строки треков (<see cref="TrackRow"/>) исполнителями и альбомами
/// за два дополнительных пакетных запроса к БД.
/// </summary>
internal sealed class TrackEnricher
{
	private readonly IDbConnectionFactory _connectionFactory;

	public TrackEnricher(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<List<Track>> EnrichAsync(IReadOnlyList<TrackRow> rows)
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
}
