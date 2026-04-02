using YamBassPlayer.Models;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Extensions;

public static class YTrackExtensions
{
	public static Track ToTrack(this YTrack track)
	{
		string artists = track.Artists != null
			? string.Join(", ", track.Artists.Select(a => a.Name))
			: "Неизвестный исполнитель";

		var firstAlbum = track.Albums?.FirstOrDefault();
		string album = firstAlbum?.Title ?? "";

		string? coverUrl = track.CoverUri is { } uri
			? $"https://{uri.TrimStart('/')}"
			: null;

		IReadOnlyList<Artist>? artistObjects = track.Artists
			?.Select(a => new Artist(a.Id, a.Name))
			.ToList();

		Album? albumInfo = firstAlbum is null ? null : new Album(firstAlbum.Id, firstAlbum.Title)
		{
			Year = firstAlbum.Year,
			CoverUrl = firstAlbum.CoverUri is { } albumUri ? $"https://{albumUri.TrimStart('/')}" : null,
			Genre = firstAlbum.Genre,
			TrackCount = firstAlbum.TrackCount,
			ArtistIds = track.Artists?.Select(a => a.Id).ToList(),
		};

		IReadOnlyList<string>? genres = firstAlbum?.Genre is { } genre
			? [genre]
			: null;

		return new Track(track.Title, artists, album, track.Id)
		{
			DurationMs = track.DurationMs,
			Year = firstAlbum?.Year,
			CoverUrl = coverUrl,
			RemoteCoverUrl = coverUrl,
			Genres = genres,
			SourceType = "yandex",
			Artists = artistObjects,
			AlbumInfo = albumInfo,
		};
	}
}
