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

		// Идентификатор трека уникален только внутри источника, поэтому все выборки и словари
		// адресуются составным ключом (источник, идентификатор): иначе одинаковые id из
		// Яндекса и локальной библиотеки слиплись бы.
		var trackKeys = rows
			.Select(r => (r.SourceId, r.TrackId))
			.Distinct()
			.ToList();

		using var connection = _connectionFactory.Create();

		// --- 1. Batch-load artists for all track keys ---
		var artistsByTrackKey = new Dictionary<(SourceId SourceId, string TrackId), List<Artist>>();
		var tidParams = trackKeys.Select((_, i) => $"(@sid{i}, @tid{i})").ToList();

		using (var cmd = connection.CreateCommand())
		{
			cmd.CommandText = $@"
				SELECT ta.SourceType, ta.TrackId, a.Id, a.Name, a.CoverUrl, a.Description
				FROM TrackArtists ta
				JOIN Artists a ON ta.ArtistId = a.Id AND ta.SourceType = a.SourceType
				WHERE (ta.SourceType, ta.TrackId) IN (VALUES {string.Join(", ", tidParams)})";
			for (int i = 0; i < trackKeys.Count; i++)
			{
				cmd.Parameters.AddWithValue($"@sid{i}", trackKeys[i].SourceId.Value);
				cmd.Parameters.AddWithValue($"@tid{i}", trackKeys[i].TrackId);
			}

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				var key = (new SourceId(reader.GetString(0)), reader.GetString(1));
				var artist = new Artist(reader.GetString(2), reader.GetString(3))
				{
					CoverUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
					Description = reader.IsDBNull(5) ? null : reader.GetString(5),
				};

				if (!artistsByTrackKey.TryGetValue(key, out List<Artist>? list))
					artistsByTrackKey[key] = list = [];
				list.Add(artist);
			}
		}

		// --- 2. Batch-load albums for all referenced album keys ---
		var albumKeys = rows
			.Where(r => r.AlbumId is not null)
			.Select(r => (r.SourceId, AlbumId: r.AlbumId!))
			.Distinct()
			.ToList();

		var albumsByKey = new Dictionary<(SourceId SourceId, string AlbumId), Album>();

		if (albumKeys.Count > 0)
		{
			var aidParams = albumKeys.Select((_, i) => $"(@asid{i}, @aid{i})").ToList();

			using var cmd = connection.CreateCommand();
			cmd.CommandText = $@"
				SELECT SourceType, Id, Title, Year, CoverUrl, Genre, TrackCount
				FROM Albums
				WHERE (SourceType, Id) IN (VALUES {string.Join(", ", aidParams)})";
			for (int i = 0; i < albumKeys.Count; i++)
			{
				cmd.Parameters.AddWithValue($"@asid{i}", albumKeys[i].SourceId.Value);
				cmd.Parameters.AddWithValue($"@aid{i}", albumKeys[i].AlbumId);
			}

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				var key = (new SourceId(reader.GetString(0)), reader.GetString(1));
				var album = new Album(reader.GetString(1), reader.GetString(2))
				{
					Year = reader.IsDBNull(3) ? null : reader.GetInt32(3),
					CoverUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
					Genre = reader.IsDBNull(5) ? null : reader.GetString(5),
					TrackCount = reader.IsDBNull(6) ? null : reader.GetInt32(6),
				};
				albumsByKey[key] = album;
			}
		}

		// --- 3. Assemble enriched Track objects ---
		var result = new List<Track>(rows.Count);
		foreach (TrackRow row in rows)
		{
			IReadOnlyList<Artist>? artists =
				artistsByTrackKey.TryGetValue((row.SourceId, row.TrackId), out List<Artist>? artistList)
					? artistList
					: null;

			Album? albumInfo = row.AlbumId is not null
				&& albumsByKey.TryGetValue((row.SourceId, row.AlbumId), out Album? album)
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
				CoverUrl = CoverMetadataResolver.ResolveLegacyCoverUrl(row.CoverUrl, row.RemoteCoverUrl, row.LocalCoverPath),
				RemoteCoverUrl = CoverMetadataResolver.ResolveRemoteCoverUrl(row.RemoteCoverUrl, row.CoverUrl),
				LocalCoverPath = CoverMetadataResolver.ResolveLocalCoverPath(row.LocalCoverPath, row.CoverUrl),
				Genres = genres,
				SourceId = row.SourceId,
				LocalFilePath = row.LocalFilePath,
				Artists = artists,
				AlbumInfo = albumInfo,
			});
		}

		return result;
	}
}
