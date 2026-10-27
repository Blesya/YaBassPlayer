using System.Net.Http;
using System.Threading;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Playlist;
using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public sealed class YandexMusicSource : IMusicSource, IEntitySearchSource, ITrackAboutSource, ILyricsSource, IRadioSource
{
    private const string OwnGroup = "Мои плейлисты";

    private readonly YandexMusicApi _api;
    private readonly AuthStorage _storage;
    private readonly string _tracksFolder;
    private readonly string _coversFolder;
    private readonly IYandexFavoriteService _favoriteService;
    private readonly IYandexRadioService _radio;
    private readonly YandexTrackAboutSource _about;
    private readonly YandexLyricsSource _lyrics;
    private List<Playlist>? _playlistsCache;
    private List<string>? _likedTrackIdsCache;
    private List<YamBassPlayer.Api.Models.Common.YResponse<YamBassPlayer.Api.Models.Playlist.YPlaylist>>? _personalPlaylistsCache;

    SourceId IMusicSource.Id => YandexMusicSourceId.Value;
    public SourceState State => _storage.IsAuthorized ? SourceState.Ready : SourceState.RequiresSetup;
    public string DisplayName => "Яндекс.Музыка";
    public bool SupportsSearch => true;
    public bool SupportsEntitySearch => true;

	public YandexMusicSource(
		YandexMusicApi api,
		AuthStorage storage,
		string tracksFolder,
		string coversFolder,
		IYandexFavoriteService favoriteService,
		IYandexRadioService radio)
	{
		ArgumentNullException.ThrowIfNull(api);
		ArgumentNullException.ThrowIfNull(storage);
		ArgumentException.ThrowIfNullOrWhiteSpace(tracksFolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(coversFolder);
		ArgumentNullException.ThrowIfNull(favoriteService);
		ArgumentNullException.ThrowIfNull(radio);

		_api = api;
		_storage = storage;
		_tracksFolder = tracksFolder;
		_coversFolder = coversFolder;
		_favoriteService = favoriteService;
		_radio = radio;
		_about = new YandexTrackAboutSource(api, storage);
		_lyrics = new YandexLyricsSource(api, storage);

		// Лайк меняет «Мои треки»: следующий запрос дерева должен увидеть новый состав.
		_favoriteService.OnFavoriteAdded += _ => InvalidatePlaylists();
		_favoriteService.OnFavoriteRemoved += _ => InvalidatePlaylists();
	}

	private void InvalidatePlaylists()
	{
		_playlistsCache = null;
		_likedTrackIdsCache = null;
	}

	public bool IsSessionActive => _radio.IsSessionActive;

	public Task<bool> StartPersonalRadioAsync(CancellationToken ct = default)
		=> _radio.StartMyWaveAsync();

	public Task<bool> StartTrackRadioAsync(TrackKey key, CancellationToken ct = default)
		=> _radio.StartTrackWaveAsync(key.TrackId);

	public async Task<RadioBatch> FetchNextBatchAsync(CancellationToken ct = default)
	{
		var (trackIds, tracks) = await _radio.FetchNextBatchAsync();
		var keys = trackIds
			.Select(id => new TrackKey(YandexMusicSourceId.Value, id))
			.ToList();
		return new RadioBatch(keys, tracks);
	}

	public Task SendTrackStartedAsync(TrackKey key, CancellationToken ct = default)
		=> _radio.SendTrackStartedAsync(key.TrackId);

	public Task SendTrackFinishedAsync(TrackKey key, double playedSeconds = 0, CancellationToken ct = default)
		=> _radio.SendTrackFinishedAsync(key.TrackId, playedSeconds);

	public Task SendTrackSkippedAsync(TrackKey key, double playedSeconds = 0, CancellationToken ct = default)
		=> _radio.SendTrackSkippedAsync(key.TrackId, playedSeconds);

	public void Reset() => _radio.Reset();

	public Task<TrackAboutInfo> GetAboutAsync(TrackKey key, bool forceRefresh = false)
		=> _about.GetAboutAsync(key, forceRefresh);

	public Task<string?> GetLyricsAsync(Track track)
		=> _lyrics.GetLyricsAsync(track);

	/// <summary>
	/// Returns a Favorite playlist, the landing "personal" mixes, the user's own playlists
	/// and the playlists the user has saved from other people.
	/// </summary>
	/// <remarks>
	/// Четыре запроса идут параллельно: последовательно они складывались в секунды
	/// ожидания. Ошибка любого из них — это ошибка обновления источника: хост оставит
	/// ветку из снимка, а не покажет частичный список как свежий.
	/// </remarks>
	public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		if (_playlistsCache is not null)
			return _playlistsCache;

		var likedTracksTask = _api.Library.GetLikedTracksAsync(_storage);
		var personalTask = _api.Playlist.GetPersonalPlaylistsAsync(_storage);
		Task<IEnumerable<Playlist>> ownTask = GetOwnPlaylistsAsync();
		Task<IEnumerable<Playlist>> likedPlaylistsTask = GetLikedPlaylistsAsync();

		await Task.WhenAll(likedTracksTask, personalTask, ownTask, likedPlaylistsTask);

		var liked = await likedTracksTask;
		int likedCount = liked.Result.Library.Tracks.Count;
		_likedTrackIdsCache = liked.Result.Library.Tracks.Select(t => t.Id).ToList();

		// Избранное инициализирует сам источник — отдельного инициализатора больше нет.
		_favoriteService.Initialize(_likedTrackIdsCache);

		var playlists = new List<Playlist>
		{
			new("favorite", "Мои треки")
			{
				SourceId = YandexMusicSourceId.Value,
				Category = PlaylistCategory.Data,
				Description = "Треки, которые вам понравились",
				TrackCount = likedCount
			},
			new("playlist-of-the-day", "Плейлист дня")
			{
				SourceId = YandexMusicSourceId.Value,
				Category = PlaylistCategory.Data,
				Description = "Подборка дня",
				TrackCount = 0
			}
		};

		// Лендинг «Персональные плейлисты»: плейлист дня, дежавю, премьера и т.п.
		List<YResponse<YamBassPlayer.Api.Models.Playlist.YPlaylist>> personalPlaylists =
			await personalTask;
		_personalPlaylistsCache = personalPlaylists;
		foreach (var yResponse in personalPlaylists)
		{
			playlists.Add(new Playlist("personal", yResponse.Result.Title)
			{
				SourceId = YandexMusicSourceId.Value,
				Category = PlaylistCategory.Data,
				Description = yResponse.Result.Description,
				TrackCount = yResponse.Result.TrackCount,
				Payload = BuildPayload(yResponse.Result.Owner?.Uid, yResponse.Result.Kind)
			});
		}

		// Собственные плейлисты и сохранённые чужие: в библиотеке методов нет,
		// собственные берём напрямую из API, сохранённые — из секции библиотеки.
		playlists.AddRange(await ownTask);
		playlists.AddRange(await likedPlaylistsTask);

		_playlistsCache = PlaylistDeduplicator.ByCacheKey(playlists);
		return _playlistsCache;
	}

	/// <summary>
	/// Собственные плейлисты пользователя (<c>/users/{uid}/playlists/list</c>).
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
			.Select(playlist => ToPlaylist(playlist, "own", OwnGroup));
	}

	private async Task<IEnumerable<Playlist>> GetLikedPlaylistsAsync()
	{
		var response = await _api.Library.GetLikedPlaylistsAsync(_storage)
			?? throw new InvalidOperationException("Библиотека плейлистов Яндекс.Музыки вернула пустой ответ");
		string? ownUid = _storage.User?.Uid;

		return (response.Result ?? [])
			.Select(item => item.Playlist)
			.Where(playlist => playlist is not null)
			// Свои плейлисты уже пришли отдельным запросом — не дублируем.
			.Where(playlist => !string.Equals(playlist.Owner?.Uid, ownUid, StringComparison.Ordinal))
			.GroupBy(playlist => $"{playlist.Owner?.Uid}:{playlist.Kind}")
			.Select(group => group.First())
			.Select(playlist => ToPlaylist(playlist, "liked"));
	}

	private static Dictionary<string, string>? BuildPayload(string? owner, string? kind)
	{
		if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(kind))
			return null;

		return new Dictionary<string, string>
		{
			["owner"] = owner,
			["kind"] = kind
		};
	}

	private static Playlist ToPlaylist(YPlaylist playlist, string kind, string? group = null)
		=> new(kind, playlist.Title)
		{
			SourceId = YandexMusicSourceId.Value,
			Category = PlaylistCategory.Data,
			Description = playlist.Description ?? string.Empty,
			TrackCount = playlist.TrackCount,
			Group = group,
			Payload = BuildPayload(playlist.Owner?.Uid, playlist.Kind)
		};

	/// <summary>
	/// Loads a page of tracks for the given playlist using offset/limit.
	/// </summary>
	public async Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			return playlist.Kind switch
			{
				"favorite" => await GetFavoriteTracksPageAsync(offset, limit),
				"playlist-of-the-day" => await GetPlaylistOfTheDailyAsync(offset, limit),
				"personal" => await GetPersonalPlaylistTracksPageAsync(playlist, offset, limit),
				"own" or "liked" => await GetYandexPlaylistTracksPageAsync(playlist, offset, limit),
				_ => []
			};
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка источника Яндекс.Музыки");
			return [];
		}
	}

	/// <summary>
	/// Returns only track IDs for the given playlist, without fetching full track metadata.
	/// </summary>
	public async Task<IReadOnlyList<string>> GetPlaylistTrackIdsAsync(Playlist playlist, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			return playlist.Kind switch
			{
				"favorite" => await GetFavoriteTrackIdsAsync(),
				"playlist-of-the-day" => await GetPlaylistOfTheDailyTrackIdsAsync(),
				"personal" => await GetPersonalPlaylistTrackIdsAsync(playlist),
				"own" or "liked" => await GetYandexPlaylistTrackIdsAsync(playlist),
				_ => []
			};
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка источника Яндекс.Музыки");
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
	public async Task<string> GetAudioFilePathAsync(
		string trackId,
		string destinationPath,
		IProgress<DownloadProgress>? progress = null,
		CancellationToken ct = default)
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

			if (!track.Available && track.Substituted is null)
				throw new TrackUnavailableException(trackId);

			// У недоступного в регионе трека Яндекс может прислать замену — играем её.
			YTrack downloadTrack = track.Available || track.Substituted is null
				? track
				: track.Substituted;

			string url = await _api.Track.GetFileLinkAsync(_storage, downloadTrack);
			string tempPath = $"{destinationPath}.{Guid.NewGuid():N}.part";

			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Get, url);
				using var fileResponse = await _storage.Provider.GetWebResponseAsync(
					request,
					HttpCompletionOption.ResponseHeadersRead);
				fileResponse.EnsureSuccessStatusCode();

				long totalBytes = fileResponse.Content.Headers.ContentLength ?? downloadTrack.FileSize;

				await using (var stream = await fileResponse.Content.ReadAsStreamAsync(ct))
				await using (var file = File.Create(tempPath))
				{
					await CopyWithProgressAsync(stream, file, totalBytes, progress, ct);
				}

				if (File.Exists(destinationPath))
					return destinationPath;

				File.Move(tempPath, destinationPath, overwrite: true);
				long finalSize = new FileInfo(destinationPath).Length;
				progress?.Report(new DownloadProgress(finalSize, totalBytes > 0 ? totalBytes : finalSize));
				return destinationPath;
			}
			finally
			{
				if (File.Exists(tempPath))
					File.Delete(tempPath);
			}
		}
		catch (TrackUnavailableException)
		{
			throw;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка источника Яндекс.Музыки");
			return string.Empty;
		}
	}

	private static async Task CopyWithProgressAsync(
		Stream source,
		Stream destination,
		long totalBytes,
		IProgress<DownloadProgress>? progress,
		CancellationToken ct)
	{
		var buffer = new byte[81920];
		long copied = 0;
		long lastIndeterminateReport = -1;
		int lastPercent = -1;

		int read;
		while ((read = await source.ReadAsync(buffer, ct)) > 0)
		{
			await destination.WriteAsync(buffer.AsMemory(0, read), ct);
			copied += read;

			if (progress is null)
				continue;

			if (totalBytes > 0)
			{
				int percent = (int)Math.Min(copied * 100 / totalBytes, 99);
				if (percent == lastPercent)
					continue;

				lastPercent = percent;
				progress.Report(new DownloadProgress(copied, totalBytes));
			}
			else if (copied - lastIndeterminateReport >= 262144)
			{
				lastIndeterminateReport = copied;
				progress.Report(new DownloadProgress(copied, 0));
			}
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
			Log.Error(ex, "Ошибка источника Яндекс.Музыки");
			return [];
		}
	}

	/// <summary>
	/// Searches Yandex Music for tracks, artists, and albums matching the query.
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
			Log.Error(ex, "Ошибка источника Яндекс.Музыки");
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
			Log.Error(ex, "Ошибка источника Яндекс.Музыки");
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
			Log.Error(ex, "Ошибка источника Яндекс.Музыки");
			return [];
		}
	}

	private async Task<IReadOnlyList<string>> GetFavoriteTrackIdsAsync()
	{
		if (_likedTrackIdsCache is not null)
			return _likedTrackIdsCache;

		var liked = await _api.Library.GetLikedTracksAsync(_storage);
		_likedTrackIdsCache = liked.Result.Library.Tracks.Select(t => t.Id).ToList();
		_favoriteService.Initialize(_likedTrackIdsCache);
		return _likedTrackIdsCache;
	}

	/// <summary>
	/// Возвращает идентификаторы треков «Плейлиста дня» — плейлист приходит из API,
	/// а не из списка плейлистов пользователя.
	/// </summary>
	private async Task<IReadOnlyList<string>> GetPlaylistOfTheDailyTrackIdsAsync()
	{
		var response = await _api.Playlist.OfTheDayAsync(_storage);
		return response.Result.Tracks.Select(t => t.Id).ToList();
	}

	private async Task<IReadOnlyList<string>> GetPersonalPlaylistTrackIdsAsync(Playlist playlist)
	{
		if (playlist.Payload is { } payload
			&& payload.TryGetValue("owner", out string? owner)
			&& payload.TryGetValue("kind", out string? kind))
		{
			var byIdentity = await _api.Playlist.GetAsync(_storage, owner, kind);
			return byIdentity?.Result?.Tracks?.Select(c => c.Track.Id).ToList() ?? [];
		}

		var playlists = _personalPlaylistsCache;
		if (playlists is null)
		{
			playlists = (await _api.Playlist.GetPersonalPlaylistsAsync(_storage)).ToList();
			_personalPlaylistsCache = playlists;
		}

		var found = playlists.FirstOrDefault(r => r.Result.Title == playlist.Title);
		return found?.Result.Tracks.Select(c => c.Track.Id).ToList() ?? [];
	}

	private async Task<YPlaylist?> FetchYandexPlaylistAsync(Playlist playlist)
	{
		if (playlist.Payload is not { } payload
			|| !payload.TryGetValue("owner", out string? ownerUid)
			|| !payload.TryGetValue("kind", out string? kind))
		{
			return null;
		}

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

	private async Task<IEnumerable<Track>> GetPersonalPlaylistTracksPageAsync(
		Playlist playlist,
		int offset,
		int limit)
	{
		IReadOnlyList<string> ids = await GetPersonalPlaylistTrackIdsAsync(playlist);
		if (ids.Count == 0)
			return [];

		var response = await _api.Track.GetAsync(_storage, ids.Skip(offset).Take(limit).ToList());
		return response?.Result?.Select(t => t.ToTrack()) ?? [];
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
}
