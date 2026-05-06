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
			? NormalizeCoverUrl(uri)
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

	private static string NormalizeCoverUrl(string rawUrl)
	{
		string normalized = rawUrl.Replace("%%", "400x400");

		if (normalized.StartsWith("//"))
			return $"https:{normalized}";

		if (!normalized.StartsWith("http://") && !normalized.StartsWith("https://"))
			return $"https://{normalized.TrimStart('/')}";

		return normalized;
	}
}
