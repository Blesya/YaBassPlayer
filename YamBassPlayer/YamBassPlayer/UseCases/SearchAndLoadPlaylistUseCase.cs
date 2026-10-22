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
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ITrackRepositoryCache _trackRepositoryCache;
	private readonly ITrackRepository _trackRepository;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IErrorHandler _errorHandler;

	public SearchAndLoadPlaylistUseCase(
		ITrackInfoProvider trackInfoProvider,
		ITrackRepositoryCache trackRepositoryCache,
		ITrackRepository trackRepository,
		ITracksPresenter tracksPresenter,
		IPlaylistsPresenter playlistsPresenter,
		IPlayStatusPresenter playStatusPresenter,
		IErrorHandler errorHandler)
	{
		_trackInfoProvider = trackInfoProvider;
		_trackRepositoryCache = trackRepositoryCache;
		_trackRepository = trackRepository;
		_tracksPresenter = tracksPresenter;
		_playlistsPresenter = playlistsPresenter;
		_playStatusPresenter = playStatusPresenter;
		_errorHandler = errorHandler;
	}

	/// <summary>
	/// Ищет треки в локальной медиатеке и загружает результаты как временный плейлист.
	/// </summary>
	public async Task SearchAndLoadAsync(
		SourceId source,
		string query,
		SearchEntityKind kind,
		Action<string> setWindowTitle)
	{
		Logging.LogBeforeCall();

		try
		{
			List<Track> tracks = (await _trackInfoProvider.SearchTracks(query, 50)).ToList();
			_trackRepositoryCache.ReplaceSearchTracks(source, tracks);

			if (tracks.Count == 0)
			{
				_playStatusPresenter.SetPlayStatus($"По запросу «{query}» ничего не найдено");
				return;
			}

			await LoadInternalAsync(source, tracks, $"Результаты поиска: {query}", setWindowTitle);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	/// <summary>Загружает треки, выбранные в окне поиска указанного источника.</summary>
	public async Task LoadSelectionAsync(
		SourceId source,
		IReadOnlyList<Track> tracks,
		Action<string> setWindowTitle)
	{
		Logging.LogBeforeCall();

		try
		{
			_trackRepositoryCache.ReplaceSearchTracks(source, tracks);
			await LoadInternalAsync(source, tracks, "Результаты поиска", setWindowTitle);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private async Task LoadInternalAsync(
		SourceId source,
		IReadOnlyList<Track> tracks,
		string description,
		Action<string> setWindowTitle)
	{
		var playlist = new Playlist("search", "Поиск")
		{
			SourceId = source,
			Category = PlaylistCategory.Transient,
			Description = description,
			TrackCount = tracks.Count
		};

		Log.Information("Открыт временный плейлист: «{Title}» ({TrackCount} треков)",
			playlist.Title, playlist.TrackCount);
		await _trackRepository.SetPlaylist(playlist);
		await _tracksPresenter.LoadTracksFor(playlist);
		setWindowTitle($"{playlist.Title} : {playlist.Description}");
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
	}
}
