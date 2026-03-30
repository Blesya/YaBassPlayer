using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Pure Yandex Music API wrapper implementing <see cref="IMusicSource"/>.
/// Does not touch the SQLite database — caching is the caller's responsibility.
/// </summary>
public sealed class YandexMusicSource : IMusicSource
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly string _tracksFolder;
	private readonly string _coversFolder;

	public string SourceId => "yandex";
	public string DisplayName => "Яндекс.Музыка";
	public bool SupportsSearch => true;
	public bool SupportsFavorites => true;

	public YandexMusicSource(
		YandexMusicApi api,
		AuthStorage storage,
		string tracksFolder,
		string coversFolder)
	{
		ArgumentNullException.ThrowIfNull(api);
		ArgumentNullException.ThrowIfNull(storage);
		ArgumentException.ThrowIfNullOrWhiteSpace(tracksFolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(coversFolder);

		_api = api;
		_storage = storage;
		_tracksFolder = tracksFolder;
		_coversFolder = coversFolder;
	}

	/// <summary>
	/// Returns a Favorite playlist and all personal (custom) playlists from Yandex Music.
	/// </summary>
	public async Task<IEnumerable<Playlist>> GetPlaylistsAsync()
	{
		try
		{
			var liked = await _api.Library.GetLikedTracksAsync(_storage);
			int likedCount = liked.Result.Library.Tracks.Count;

			var playlists = new List<Playlist>
			{
				new Playlist("Мои треки", PlaylistType.Favorite)
				{
					Description = "Треки, которые вам понравились",
					TrackCount = likedCount
				}
			};

			var personalPlaylists = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);
			foreach (var yResponse in personalPlaylists)
			{
				playlists.Add(new Playlist(yResponse.Result.Title, PlaylistType.Custom)
				{
					Description = yResponse.Result.Description,
					TrackCount = yResponse.Result.TrackCount
				});
			}

			return playlists;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}

	/// <summary>
	/// Loads a page of tracks for the given playlist using offset/limit.
	/// Supports <see cref="PlaylistType.Favorite"/>, <see cref="PlaylistType.Custom"/>,
	/// and <see cref="PlaylistType.PlaylistOfTheDaily"/>.
	/// </summary>
	public async Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit)
	{
		try
		{
			return playlist.Type switch
			{
				PlaylistType.Favorite => await GetFavoriteTracksPageAsync(offset, limit),
				PlaylistType.Custom => await GetCustomPlaylistTracksPageAsync(playlist.PlaylistName, offset, limit),
				PlaylistType.PlaylistOfTheDaily => await GetPlaylistOfTheDailyAsync(offset, limit),
				_ => []
			};
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}

	/// <summary>
	/// Fetches a single track by its Yandex track ID. Returns null if not found.
	/// </summary>
	public async Task<Track?> GetTrackAsync(string trackId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

		var response = await _api.Track.GetAsync(_storage, trackId);
		var yTrack = response?.Result?.FirstOrDefault();
		return yTrack?.ToTrack();
	}

	/// <summary>
	/// Batch-fetches tracks by their Yandex track IDs in a single API call.
	/// </summary>
	public async Task<IEnumerable<Track>> GetTracksByIdsAsync(IEnumerable<string> ids)
	{
		var idsList = ids.ToList();
		if (idsList.Count == 0)
			return [];

		var response = await _api.Track.GetAsync(_storage, idsList);
		return response?.Result?.Select(t => t.ToTrack()) ?? [];
	}

	/// <summary>
	/// Downloads the audio file to <paramref name="destinationPath"/> if it does not already exist.
	/// Returns the file path on success, or <see cref="string.Empty"/> on failure.
	/// </summary>
	public async Task<string> GetAudioFilePathAsync(string trackId, string destinationPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

		if (File.Exists(destinationPath))
			return destinationPath;

		try
		{
			var response = await _api.Track.GetAsync(_storage, trackId);
			var track = response?.Result?.FirstOrDefault();
			if (track == null)
				return string.Empty;

			await _api.Track.ExtractToFileAsync(_storage, track, destinationPath);
			return destinationPath;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return string.Empty;
		}
	}

	/// <summary>
	/// Returns a normalized cover URL for the given track ID, or null if unavailable.
	/// </summary>
	public async Task<string?> GetCoverUrlAsync(string trackId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

		var response = await _api.Track.GetAsync(_storage, trackId);
		var track = response?.Result?.FirstOrDefault();
		if (track == null)
			return null;

		string? uri = track.CoverUri;
		if (string.IsNullOrWhiteSpace(uri))
			return null;

		return NormalizeCoverUrl(uri);
	}

	/// <summary>
	/// Searches Yandex Music for tracks matching the query. Returns up to 20 results.
	/// </summary>
	public async Task<IEnumerable<Track>> SearchAsync(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
			return [];

		try
		{
			var response = await _api.Search.TrackAsync(_storage, query, 0, 20);
			var results = response?.Result?.Tracks?.Results;

			if (results == null || results.Count == 0)
				return [];

			return results.Select(t => t.ToTrack());
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}

	private async Task<IEnumerable<Track>> GetFavoriteTracksPageAsync(int offset, int limit)
	{
		var liked = await _api.Library.GetLikedTracksAsync(_storage);
		var ids = liked.Result.Library.Tracks
			.Skip(offset)
			.Take(limit)
			.Select(t => t.Id)
			.ToList();

		if (ids.Count == 0)
			return [];

		var response = await _api.Track.GetAsync(_storage, ids);
		return response?.Result?.Select(t => t.ToTrack()) ?? [];
	}

	private async Task<IEnumerable<Track>> GetPlaylistOfTheDailyAsync(int offset, int limit)
	{
		var response = await _api.Playlist.OfTheDayAsync(_storage);
		var ids = response.Result.Tracks
			.Skip(offset)
			.Take(limit)
			.Select(t => t.Id)
			.ToList();

		if (ids.Count == 0)
			return [];

		var tracks = await _api.Track.GetAsync(_storage, ids);
		return tracks?.Result?.Select(t => t.ToTrack()) ?? [];
	}

	private async Task<IEnumerable<Track>> GetCustomPlaylistTracksPageAsync(
		string playlistName,
		int offset,
		int limit)
	{
		var playlists = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);
		var found = playlists.FirstOrDefault(r => r.Result.Title == playlistName);
		if (found == null)
			return [];

		return found.Result.Tracks
			.Skip(offset)
			.Take(limit)
			.Select(c => c.Track.ToTrack());
	}

	// Mirrors CoverProvider.NormalizeCoverUrl — ensures a complete https:// URL.
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
