using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;

namespace YamBassPlayer.UseCases;

/// <summary>
/// Прикладной сценарий поиска: выполняет запрос к источнику, складывает результаты
/// в <see cref="ITrackRepositoryCache"/> и загружает их как временный плейлист.
/// Открытие модальных окон поиска остаётся за вызывающей стороной.
/// </summary>
public sealed class SearchAndLoadPlaylistUseCase
{
	private readonly ISourceSearchService _sourceSearchService;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ITrackRepositoryCache _trackRepositoryCache;
	private readonly ITrackRepository _trackRepository;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IErrorHandler _errorHandler;

	public SearchAndLoadPlaylistUseCase(
		ISourceSearchService sourceSearchService,
		ITrackInfoProvider trackInfoProvider,
		ITrackRepositoryCache trackRepositoryCache,
		ITrackRepository trackRepository,
		ITracksPresenter tracksPresenter,
		IPlaylistsPresenter playlistsPresenter,
		IPlayStatusPresenter playStatusPresenter,
		IErrorHandler errorHandler)
	{
		_sourceSearchService = sourceSearchService;
		_trackInfoProvider = trackInfoProvider;
		_trackRepositoryCache = trackRepositoryCache;
		_trackRepository = trackRepository;
		_tracksPresenter = tracksPresenter;
		_playlistsPresenter = playlistsPresenter;
		_playStatusPresenter = playStatusPresenter;
		_errorHandler = errorHandler;
	}

	/// <summary>
	/// Выполняет поиск (треки/первый исполнитель/первый альбом для Яндекс.Музыки,
	/// либо локальный поиск) и загружает результаты как временный плейлист.
	/// </summary>
	public async Task SearchAndLoadAsync(
		string source,
		string query,
		SearchEntityKind kind,
		Action<string> setWindowTitle)
	{
		Logging.LogBeforeCall();

		try
		{
			bool isYandex = string.Equals(source, SourceIds.Yandex, StringComparison.OrdinalIgnoreCase);

			List<Track> tracks;
			if (isYandex)
			{
				if (kind == SearchEntityKind.Artist)
				{
					var artistTracks = await GetFirstArtistTracksAsync(query);
					if (artistTracks is null) return;
					tracks = artistTracks;
				}
				else if (kind == SearchEntityKind.Album)
				{
					var albumTracks = await GetFirstAlbumTracksAsync(query);
					if (albumTracks is null) return;
					tracks = albumTracks;
				}
				else
				{
					tracks = (await _sourceSearchService.SearchAsync(SourceIds.Yandex, query, 50)).ToList();
				}

				await SaveAndCacheYandexAsync(tracks);
			}
			else
			{
				tracks = (await _trackInfoProvider.SearchTracks(query, 50)).ToList();
				_trackRepositoryCache.ReplaceLocalSearchTracks(tracks);
			}

			if (tracks.Count == 0)
			{
				_playStatusPresenter.SetPlayStatus($"По запросу «{query}» ничего не найдено");
				return;
			}

			await LoadInternalAsync(tracks, source, $"Результаты поиска: {query}", setWindowTitle);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	/// <summary>Загружает треки, выбранные в окне поиска по Яндекс.Музыке.</summary>
	public async Task LoadYandexSelectionAsync(IReadOnlyList<Track> tracks, Action<string> setWindowTitle)
	{
		Logging.LogBeforeCall();

		try
		{
			await SaveAndCacheYandexAsync(tracks);
			await LoadInternalAsync(tracks, SourceIds.Yandex, "Результаты поиска по Яндекс.Музыке", setWindowTitle);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	/// <summary>Загружает треки, выбранные в окне локального поиска.</summary>
	public async Task LoadLocalSelectionAsync(IReadOnlyList<Track> tracks, Action<string> setWindowTitle)
	{
		Logging.LogBeforeCall();

		try
		{
			_trackRepositoryCache.ReplaceLocalSearchTracks(tracks);
			await LoadInternalAsync(tracks, SourceIds.Local, "Результаты локального поиска", setWindowTitle);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private async Task SaveAndCacheYandexAsync(IReadOnlyList<Track> tracks)
	{
		foreach (var track in tracks)
			await _trackInfoProvider.SaveAsync(track);

		_trackRepositoryCache.ReplaceYandexSearchTracks(tracks);
	}

	private async Task LoadInternalAsync(
		IReadOnlyList<Track> tracks,
		string source,
		string description,
		Action<string> setWindowTitle)
	{
		bool isYandex = string.Equals(source, SourceIds.Yandex, StringComparison.OrdinalIgnoreCase);

		var playlist = new Playlist(
			isYandex ? "Поиск по ЯМ" : "Локальный поиск",
			isYandex ? PlaylistType.YandexSearch : PlaylistType.LocalSearch)
		{
			Description = description,
			TrackCount = tracks.Count,
			SourceId = source,
			ParentTag = isYandex ? SourceIds.Yandex : SourceIds.Local
		};

		Log.Information("Открыт временный плейлист: «{PlaylistName}» ({TrackCount} треков)",
			playlist.PlaylistName, playlist.TrackCount);
		await _trackRepository.SetPlaylist(playlist);
		await _tracksPresenter.LoadTracksFor(playlist);
		setWindowTitle($"{playlist.PlaylistName} : {playlist.Description}");
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
	}

	/// <summary>
	/// Возвращает треки первого найденного исполнителя, либо null (статус уже выставлен),
	/// если исполнитель по запросу не найден.
	/// </summary>
	private async Task<List<Track>?> GetFirstArtistTracksAsync(string query)
	{
		var result = await _sourceSearchService.SearchAllAsync(SourceIds.Yandex, query, 20);
		var artist = result.Artists.FirstOrDefault();
		if (artist is null)
		{
			_playStatusPresenter.SetPlayStatus($"Исполнитель по запросу «{query}» не найден");
			return null;
		}

		return (await _sourceSearchService.GetArtistTracksAsync(SourceIds.Yandex, artist.Id)).ToList();
	}

	/// <summary>
	/// Возвращает треки первого найденного альбома, либо null (статус уже выставлен),
	/// если альбом по запросу не найден.
	/// </summary>
	private async Task<List<Track>?> GetFirstAlbumTracksAsync(string query)
	{
		var result = await _sourceSearchService.SearchAllAsync(SourceIds.Yandex, query, 20);
		var album = result.Albums.FirstOrDefault();
		if (album is null)
		{
			_playStatusPresenter.SetPlayStatus($"Альбом по запросу «{query}» не найден");
			return null;
		}

		return (await _sourceSearchService.GetAlbumTracksAsync(SourceIds.Yandex, album.Id)).ToList();
	}
}
