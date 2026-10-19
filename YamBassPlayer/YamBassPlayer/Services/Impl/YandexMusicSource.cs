using System.Net.Http;
using System.Threading;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Playlist;

namespace YamBassPlayer.Services.Impl;

public sealed class YandexMusicSource : IMusicSource, IEntitySearchSource
{
    private readonly YandexMusicApi _api;
    private readonly AuthStorage _storage;
    private readonly string _tracksFolder;
    private readonly string _coversFolder;
    private List<Playlist>? _playlistsCache;
    private List<string>? _likedTrackIdsCache;
    private List<Yandex.Music.Api.Models.Common.YResponse<Yandex.Music.Api.Models.Playlist.YPlaylist>>? _personalPlaylistsCache;

    public string SourceId => SourceIds.Yandex;
    public string DisplayName => "Яндекс.Музыка";
    public bool SupportsSearch => true;
    public bool SupportsFavorites => true;
    public bool SupportsEntitySearch => true;

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
	/// Returns a Favorite playlist, the landing "personal" mixes, the user's own playlists
	/// and the playlists the user has saved from other people.
	/// </summary>
	public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(CancellationToken ct = default)
    {
        Logging.LogBeforeCall();

        ct.ThrowIfCancellationRequested();
        if (_playlistsCache is not null)
            return _playlistsCache;

        try
        {
            var liked = await _api.Library.GetLikedTracksAsync(_storage);
            int likedCount = liked.Result.Library.Tracks.Count;
            _likedTrackIdsCache = liked.Result.Library.Tracks.Select(t => t.Id).ToList();

            var playlists = new List<Playlist>
            {
                new Playlist("Мои треки", PlaylistType.Favorite)
                {
                    Description = "Треки, которые вам понравились",
                    TrackCount = likedCount,
                    SourceId = SourceIds.Yandex
                }
            };

            // Лендинг «Персональные плейлисты»: плейлист дня, дежавю, премьера и т.п.
            var personalPlaylists = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);
            _personalPlaylistsCache = personalPlaylists.ToList();
            foreach (var yResponse in personalPlaylists)
            {
                playlists.Add(new Playlist(yResponse.Result.Title, PlaylistType.Custom)
                {
                    Description = yResponse.Result.Description,
                    TrackCount = yResponse.Result.TrackCount,
                    SourceId = SourceIds.Yandex
                });
            }

            // Собственные плейлисты и сохранённые чужие: в библиотеке методов нет,
            // собственные берём напрямую из API, сохранённые — из секции библиотеки.
            playlists.AddRange(await GetOwnPlaylistsSafeAsync());
            playlists.AddRange(await GetLikedPlaylistsSafeAsync());

            _playlistsCache = playlists;
            Logging.LogAfterCall();
            return _playlistsCache;
        }
        catch (Exception ex)
        {
            ex.Handle();
            return [];
        }
    }

	/// <summary>
	/// Собственные плейлисты пользователя (<c>/users/{uid}/playlists/list</c>).
	/// В KM.Yandex.Music.Api метода нет, поэтому идём через её же провайдер запросов,
	/// чтобы сохранить авторизацию, прокси и десериализацию моделей.
	/// </summary>
	private async Task<IEnumerable<Playlist>> GetOwnPlaylistsAsync()
	{
		string? uid = _storage.User?.Uid;

		// После входа по QR библиотека не заполняет storage.User — дозапрашиваем аккаунт.
		if (string.IsNullOrWhiteSpace(uid))
		{
			var auth = await _api.User.GetUserAuthAsync(_storage);
			if (auth?.Result?.Account is { } account)
			{
				_storage.User = account;
				uid = account.Uid;
			}
		}

		if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(_storage.Token))
			return [];

		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			$"https://api.music.yandex.net/users/{uid}/playlists/list");
		request.Headers.TryAddWithoutValidation("Authorization", $"OAuth {_storage.Token}");

		using var response = await _storage.Provider.GetWebResponseAsync(request);
		var data = await _storage.Provider
			.GetDataFromResponseAsync<YResponse<List<YPlaylist>>>(_api, response);

		return (data?.Result ?? [])
			.Select(playlist => ToPlaylist(playlist, PlaylistType.YandexOwnPlaylist));
	}

	private async Task<IEnumerable<Playlist>> GetLikedPlaylistsAsync()
	{
		var response = await _api.Library.GetLikedPlaylistsAsync(_storage);
		string? ownUid = _storage.User?.Uid;

		return (response?.Result ?? [])
			.Select(item => item.Playlist)
			.Where(playlist => playlist is not null)
			// Свои плейлисты уже пришли отдельным запросом — не дублируем.
			.Where(playlist => !string.Equals(playlist.Owner?.Uid, ownUid, StringComparison.Ordinal))
			.GroupBy(playlist => $"{playlist.Owner?.Uid}:{playlist.Kind}")
			.Select(group => group.First())
			.Select(playlist => ToPlaylist(playlist, PlaylistType.YandexLikedPlaylist));
	}

	private async Task<IEnumerable<Playlist>> GetOwnPlaylistsSafeAsync()
	{
		try
		{
			return await GetOwnPlaylistsAsync();
		}
		catch (Exception ex)
		{
			// Список плейлистов не должен пропадать целиком из-за одного запроса.
			Log.Warning(ex, "Не удалось получить собственные плейлисты Яндекс.Музыки");
			return [];
		}
	}

	private async Task<IEnumerable<Playlist>> GetLikedPlaylistsSafeAsync()
	{
		try
		{
			return await GetLikedPlaylistsAsync();
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось получить сохранённые плейлисты Яндекс.Музыки");
			return [];
		}
	}

	private static Playlist ToPlaylist(YPlaylist playlist, PlaylistType type)
		=> new(playlist.Title, type)
		{
			Description = playlist.Description ?? string.Empty,
			TrackCount = playlist.TrackCount,
			SourceId = SourceIds.Yandex,
			Payload = new PlaylistPayload
			{
				YandexOwnerUid = playlist.Owner?.Uid,
				YandexKind = playlist.Kind
			}
		};

	/// <summary>
	/// Loads a page of tracks for the given playlist using offset/limit.
	/// Supports <see cref="PlaylistType.Favorite"/>, <see cref="PlaylistType.Custom"/>,
	/// <see cref="PlaylistType.YandexOwnPlaylist"/>, <see cref="PlaylistType.YandexLikedPlaylist"/>
	/// and <see cref="PlaylistType.PlaylistOfTheDaily"/>.
	/// </summary>
	public async Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			return playlist.Type switch
			{
				PlaylistType.Favorite => await GetFavoriteTracksPageAsync(offset, limit),
				PlaylistType.Custom => await GetCustomPlaylistTracksPageAsync(playlist.PlaylistName, offset, limit),
				PlaylistType.YandexOwnPlaylist or PlaylistType.YandexLikedPlaylist
					=> await GetYandexPlaylistTracksPageAsync(playlist, offset, limit),
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
	/// Returns only track IDs for the given playlist, without fetching full track metadata.
	/// For <see cref="PlaylistType.Favorite"/> uses cached liked track IDs.
	/// For <see cref="PlaylistType.Custom"/> extracts IDs from cached personal playlists.
	/// </summary>
	public async Task<IReadOnlyList<string>> GetPlaylistTrackIdsAsync(Playlist playlist, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			return playlist.Type switch
			{
				PlaylistType.Favorite => await GetFavoriteTrackIdsAsync(),
				PlaylistType.Custom => await GetCustomPlaylistTrackIdsAsync(playlist.PlaylistName),
				PlaylistType.YandexOwnPlaylist or PlaylistType.YandexLikedPlaylist
					=> await GetYandexPlaylistTrackIdsAsync(playlist),
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
	public async Task<Track?> GetTrackAsync(string trackId, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

		var response = await _api.Track.GetAsync(_storage, trackId);
		var yTrack = response?.Result?.FirstOrDefault();
		return yTrack?.ToTrack();
	}

	/// <summary>
	/// Batch-fetches tracks by their Yandex track IDs in a single API call.
	/// </summary>
	public async Task<IEnumerable<Track>> GetTracksByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
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
	public async Task<string> GetAudioFilePathAsync(string trackId, string destinationPath, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
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
	public async Task<string?> GetCoverUrlAsync(string trackId, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

		var response = await _api.Track.GetAsync(_storage, trackId);
		var track = response?.Result?.FirstOrDefault();
		if (track == null)
			return null;

		string? uri = track.CoverUri;
		if (string.IsNullOrWhiteSpace(uri))
			return null;

		return CoverUrl.Normalize(uri);
	}

	/// <summary>
	/// Searches Yandex Music for tracks matching the query. Returns up to 20 results.
	/// </summary>
	public async Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
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

	/// <summary>
	/// Searches Yandex Music for tracks, artists, and albums matching the query.
	/// Returns up to <paramref name="maxResults"/> results per type.
	/// </summary>
	public async Task<SearchResult> SearchAllAsync(string query, int maxResults, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
			return new SearchResult();

		try
		{
			var response = await _api.Search.SearchAsync(_storage, query, YSearchType.All, 0, maxResults);
			var result = response?.Result;
			if (result == null)
				return new SearchResult();

			return new SearchResult
			{
				Tracks = result.Tracks?.Results?.Select(t => t.ToTrack()).ToList() ?? [],
				Artists = result.Artists?.Results?.Select(a => a.ToArtist()).ToList() ?? [],
				Albums = result.Albums?.Results?.Select(a => a.ToAlbum()).ToList() ?? [],
			};
		}
		catch (Exception ex)
		{
			ex.Handle();
			return new SearchResult();
		}
	}

	/// <summary>
	/// Returns the top tracks of the given artist, limited to a single page.
	/// </summary>
	public async Task<IEnumerable<Track>> GetArtistTracksAsync(string artistId, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ArgumentException.ThrowIfNullOrWhiteSpace(artistId);

		try
		{
			var response = await _api.Artist.GetTracksAsync(_storage, artistId, 0, 50);
			return response?.Result?.Tracks?.Select(t => t.ToTrack()) ?? [];
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}

	/// <summary>
	/// Returns all tracks of the given album, flattening its volumes.
	/// </summary>
	public async Task<IEnumerable<Track>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		ArgumentException.ThrowIfNullOrWhiteSpace(albumId);

		try
		{
			var response = await _api.Album.GetAsync(_storage, albumId);
			var album = response?.Result;
			if (album?.Volumes == null)
				return [];

			return album.Volumes
				.SelectMany(volume => volume)
				.Select(t => t.ToTrack())
				.ToList();
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}

	private async Task<IReadOnlyList<string>> GetFavoriteTrackIdsAsync()
	{
		if (_likedTrackIdsCache is not null)
			return _likedTrackIdsCache;

		var liked = await _api.Library.GetLikedTracksAsync(_storage);
		_likedTrackIdsCache = liked.Result.Library.Tracks.Select(t => t.Id).ToList();
		return _likedTrackIdsCache;
	}

	private async Task<IReadOnlyList<string>> GetCustomPlaylistTrackIdsAsync(string playlistName)
	{
		var playlists = _personalPlaylistsCache;
		if (playlists is null)
		{
			playlists = (await _api.Playlist.GetPersonalPlaylistsAsync(_storage)).ToList();
			_personalPlaylistsCache = playlists;
		}

		var found = playlists.FirstOrDefault(r => r.Result.Title == playlistName);
		if (found == null)
			return [];

		return found.Result.Tracks.Select(c => c.Track.Id).ToList();
	}

	private async Task<YPlaylist?> FetchYandexPlaylistAsync(Playlist playlist)
	{
		string? ownerUid = playlist.Payload?.YandexOwnerUid;
		string? kind = playlist.Payload?.YandexKind;
		if (string.IsNullOrWhiteSpace(ownerUid) || string.IsNullOrWhiteSpace(kind))
			return null;

		var response = await _api.Playlist.GetAsync(_storage, ownerUid, kind);
		return response?.Result;
	}

	private async Task<IEnumerable<Track>> GetYandexPlaylistTracksPageAsync(
		Playlist playlist,
		int offset,
		int limit)
	{
		var yandexPlaylist = await FetchYandexPlaylistAsync(playlist);
		if (yandexPlaylist?.Tracks is null)
			return [];

		return yandexPlaylist.Tracks
			.Skip(offset)
			.Take(limit)
			.Select(container => container.Track.ToTrack());
	}

	private async Task<IReadOnlyList<string>> GetYandexPlaylistTrackIdsAsync(Playlist playlist)
	{
		var yandexPlaylist = await FetchYandexPlaylistAsync(playlist);
		return yandexPlaylist?.Tracks?.Select(container => container.Track.Id).ToList() ?? [];
	}

	private async Task<IEnumerable<Track>> GetFavoriteTracksPageAsync(int offset, int limit)
	{
		var ids = (await GetFavoriteTrackIdsAsync())
			.Skip(offset)
			.Take(limit)
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
	{		var playlists = _personalPlaylistsCache;
		if (playlists is null)
			playlists = (await _api.Playlist.GetPersonalPlaylistsAsync(_storage)).ToList();

		var found = playlists.FirstOrDefault(r => r.Result.Title == playlistName);
		if (found == null)
			return [];

		return found.Result.Tracks
			.Skip(offset)
			.Take(limit)
			.Select(c => c.Track.ToTrack());
	}

}
