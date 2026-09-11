using YamBassPlayer.Models;
using Yandex.Music.Api.Models.Search.Album;
using Yandex.Music.Api.Models.Search.Artist;

namespace YamBassPlayer.Extensions;

public static class EntitySearchExtensions
{
	public static Artist ToArtist(this YSearchArtistModel artist)
	{
		return new Artist(artist.Id, artist.Name);
	}

	public static Album ToAlbum(this YSearchAlbumModel album)
	{
		string? coverUrl = album.CoverUri is { } uri
			? CoverUrl.Normalize(uri)
			: null;

		return new Album(album.Id, album.Title)
		{
			Year = album.Year,
			CoverUrl = coverUrl,
			Genre = album.Genre,
			TrackCount = album.TrackCount,
			ArtistIds = album.Artists?.Select(a => a.Id).ToList(),
		};
	}
}
