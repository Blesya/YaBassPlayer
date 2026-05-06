using Autofac;
using System.Threading;
using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Coordinates all presenter interactions, playback, and keyboard shortcuts.
/// Extracted from MainWindow to reduce its responsibility to pure UI composition.
/// </summary>
public sealed class MainWindowCoordinator : IDisposable
{
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IPlaybackPresenter _playbackPresenter;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ISourceSearchService _sourceSearchService;
	private readonly ITrackRepository _trackRepository;
	private readonly ITrackRepositoryCache _trackRepositoryCache;
	private readonly IListenTimer _listenTimer;
	private readonly IAudioPlayer _audioPlayer;
	private readonly IEventBus _eventBus;
	private Action<TrackChangedEvent>? _onTrackChangedHandler;
	private readonly IEqualizerPresenter _equalizerPresenter;
	private readonly ILocalSearchPresenter _localSearchPresenter;
	private readonly IYandexSearchPresenter _yandexSearchPresenter;
	private readonly IDatabaseStatisticsPresenter _dbStatsPresenter;
	private readonly INowPlayingPresenter _nowPlayingPresenter;
	private readonly ILargeTrackInfoPresenter _largeTrackInfoPresenter;
	private readonly IMyWavePresenter _myWavePresenter;
	private readonly IMyWaveWindowPresenter _myWaveWindowPresenter;
	private readonly ICommandInputView _commandInputView;
	private readonly ITrackFavoriteService _trackFavoriteService;
	private readonly ITrackSourceDetector _trackSourceDetector;
	private CancellationTokenSource? _startupCts;
	private Window _window = null!; // Set later via SetWindow()

	public MainWindowCoordinator(
		IPlaylistsPresenter playlistsPresenter,
		ITracksPresenter tracksPresenter,
		IPlayStatusPresenter playStatusPresenter,
		IEqualizerPresenter equalizerPresenter,
		ILocalSearchPresenter localSearchPresenter,
		IYandexSearchPresenter yandexSearchPresenter,
		IDatabaseStatisticsPresenter dbStatsPresenter,
		INowPlayingPresenter nowPlayingPresenter,
		ILargeTrackInfoPresenter largeTrackInfoPresenter,
		IMyWavePresenter myWavePresenter,
		IMyWaveWindowPresenter myWaveWindowPresenter,
		ITrackInfoPanelPresenter trackInfoPanelPresenter,
		ICommandInputView commandInputView,
		IPlaybackPresenter playbackPresenter,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		ISourceSearchService sourceSearchService,
		ITrackRepository trackRepository,
		ITrackRepositoryCache trackRepositoryCache,
		IListenTimer listenTimer,
		IAudioPlayer audioPlayer,
		IEventBus eventBus,
		ITrackFavoriteService trackFavoriteService,
		ITrackSourceDetector trackSourceDetector)
	{
		_playlistsPresenter = playlistsPresenter;
		_tracksPresenter = tracksPresenter;
		_playStatusPresenter = playStatusPresenter;
		_equalizerPresenter = equalizerPresenter;
		_localSearchPresenter = localSearchPresenter;
		_yandexSearchPresenter = yandexSearchPresenter;
		_dbStatsPresenter = dbStatsPresenter;
		_nowPlayingPresenter = nowPlayingPresenter;
		_largeTrackInfoPresenter = largeTrackInfoPresenter;
		_myWavePresenter = myWavePresenter;
		_myWaveWindowPresenter = myWaveWindowPresenter;
		_commandInputView = commandInputView;
		_playbackPresenter = playbackPresenter;
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_sourceSearchService = sourceSearchService;
		_trackRepository = trackRepository;
		_trackRepositoryCache = trackRepositoryCache;
		_listenTimer = listenTimer;
		_audioPlayer = audioPlayer;
		_eventBus = eventBus;
		_trackFavoriteService = trackFavoriteService;
		_trackSourceDetector = trackSourceDetector;
	}

	/// <summary>
	/// Sets the window reference for title updates and keyboard shortcuts.
	/// Must be called before WireEvents().
	/// </summary>
	public void SetWindow(Window window)
	{
		_window = window ?? throw new ArgumentNullException(nameof(window));
	}

	/// <summary>
	/// Wires all presenter events, playback handlers, and keyboard shortcuts.
	/// Call once after MainWindow constructs its child views.
	/// </summary>
	public void WireEvents(Window splashScreen)
	{
		_startupCts = new CancellationTokenSource();
		_onTrackChangedHandler = e => OnTrackForPlaySelected(e.TrackId);
		_eventBus.Subscribe(_onTrackChangedHandler);

		_playStatusPresenter.OnStopClicked += StopPlayback;
		_playStatusPresenter.OnPlayClicked += TogglePlayPause;
		_playStatusPresenter.OnPrevClicked += PrevTrack;
		_playStatusPresenter.OnNextClicked += NextTrack;
		_playStatusPresenter.OnRestartClicked += RestartTrack;

		_playlistsPresenter.PlaylistChosen += OnPlaylistChosen;

		_playlistsPresenter.PlaylistChosen += _ =>
		{
			Application.Top.Remove(splashScreen);
		};

		_playStatusPresenter.OnQueueClicked += ShowCurrentQueue;
		_playStatusPresenter.OnPlaybackModeToggled += OnPlaybackModeToggled;

		_eventBus.Subscribe((PlayPauseCommandEvent _) => TogglePlayPause());
		_eventBus.Subscribe((ResumeCommandEvent _) => ResumePlayback());
		_eventBus.Subscribe((PauseCommandEvent _) => PausePlayback());
		_eventBus.Subscribe((StopCommandEvent _) => StopPlayback());
		_eventBus.Subscribe((NextCommandEvent _) => NextTrack());
		_eventBus.Subscribe((PreviousCommandEvent _) => PrevTrack());
		_eventBus.Subscribe((RestartCommandEvent _) => RestartTrack());
		_eventBus.Subscribe((SeekCommandEvent e) => SeekTo(e.Percent));
		_eventBus.Subscribe((ShuffleCommandEvent e) => SetShuffle(e.Shuffle));
		_eventBus.Subscribe((PlayTrackAtCommandEvent e) => PlayTrackAt(e.Index));
		_eventBus.Subscribe((SearchCommandEvent e) => RunSearchAsync(e.Source, e.Query, e.Kind));
		_eventBus.Subscribe((LikeCommandEvent e) => ToggleFavoriteCommandAsync(e.SourceId, e.TrackId));
		_eventBus.Subscribe((HelpCommandEvent e) => HelpDialog.Show("Справка по командам", e.HelpText));

		_audioPlayer.OnPreloadRequested += OnPreloadNextTrack;

		_window.KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.F5) { _nowPlayingPresenter.ShowNowPlaying(); e.Handled = true; }
			if (e.KeyEvent.Key == Key.F8) { _largeTrackInfoPresenter.ShowLargeTrackInfo(); e.Handled = true; }
			if (e.KeyEvent.Key == Key.F9) { ShowMyWave(); e.Handled = true; }
			if (e.KeyEvent.Key == (Key)(int)'~' || e.KeyEvent.Key == (Key)(int)'ё' || e.KeyEvent.Key == (Key)(int)'Ё')
			{
				_commandInputView.FocusInput();
				e.Handled = true;
			}
		};
	}

	// ── Playback ──────────────────────────────────────────────────────────

	private async void OnTrackForPlaySelected(string trackId)
	{
		try { await _playbackPresenter.PlaySelectedTrackAsync(trackId); }
		catch (Exception ex) { ex.Handle(); }
	}

	private async void OnPlaylistChosen(Playlist playlist)
	{
		_playbackPresenter.SetPlaylistType(playlist.Type);
		await _tracksPresenter.LoadTracksFor(playlist);
		_window.Title = $"{playlist.PlaylistName} : {playlist.Description}";
	}

	private async void OnPreloadNextTrack(object? sender, EventArgs e)
	{
		try { await _playbackPresenter.PreloadNextTrackAsync(); }
		catch (Exception ex) { ex.Handle(); }
	}

	private void OnPlaybackModeToggled()
	{
		_playbackQueue.Mode = _playbackQueue.Mode == PlaybackMode.Shuffle
			? PlaybackMode.Sequential
			: PlaybackMode.Shuffle;
		_playStatusPresenter.SetPlaybackMode(_playbackQueue.Mode);
	}

	// ── Общие обработчики воспроизведения (кнопки + командные интенты) ─────

	private void TogglePlayPause()
	{
		if (_audioPlayer.IsPlayed)
		{
			_audioPlayer.Pause();
			_listenTimer.OnPause();
			return;
		}
		_audioPlayer.Resume();
		_listenTimer.OnResume();
	}

	private void ResumePlayback()
	{
		_audioPlayer.Resume();
		_listenTimer.OnResume();
	}

	private void PausePlayback()
	{
		_audioPlayer.Pause();
		_listenTimer.OnPause();
	}

	private void StopPlayback()
	{
		_audioPlayer.Stop();
		_listenTimer.OnTrackStopOrChange();
	}

	private void NextTrack()
	{
		_playbackPresenter.MarkMyWaveSkipPending();
		_playbackQueue.Next();
	}

	private void PrevTrack()
	{
		_playbackQueue.Previous();
	}

	private void RestartTrack()
	{
		_audioPlayer.SeekToPercent(0);
		_audioPlayer.Resume();
		_listenTimer.OnResume();
	}

	private void SeekTo(int percent)
		=> _audioPlayer.SeekToPercent(percent);

	private void SetShuffle(bool shuffle)
	{
		_playbackQueue.Mode = shuffle ? PlaybackMode.Shuffle : PlaybackMode.Sequential;
		_playStatusPresenter.SetPlaybackMode(_playbackQueue.Mode);
	}

	private void PlayTrackAt(int index)
	{
		var trackIds = _trackRepository.GetAllTrackIds();
		if (index < 0 || index >= trackIds.Count)
			return;

		_playbackQueue.SetQueue(trackIds, index);
	}

	// ── Queue ─────────────────────────────────────────────────────────────

	private async void ShowCurrentQueue()
	{
		try
		{
			var trackIds = _playbackQueue.TrackIds;
			if (trackIds.Count == 0)
			{
				_playStatusPresenter.SetPlayStatus("Очередь воспроизведения пуста");
				return;
			}

			_trackRepositoryCache.ReplaceQueueTrackIds(trackIds);
			var queuePlaylist = new Playlist("Текущая очередь", PlaylistType.Queue)
			{
				Description = "Текущая очередь воспроизведения",
				TrackCount = trackIds.Count
			};

			await _tracksPresenter.LoadTracksFor(queuePlaylist);
			_window.Title = $"{queuePlaylist.PlaylistName} : {queuePlaylist.Description}";
			_playlistsPresenter.NotifyTransientPlaylistActive(queuePlaylist);
		}
		catch (Exception ex) { ex.Handle(); }
	}

	// ── Search ────────────────────────────────────────────────────────────

	public async void ToggleFavoriteCommandAsync(string sourceId, string trackId)
	{
		try
		{
			if (!_trackFavoriteService.SupportsSource(sourceId))
			{
				_playStatusPresenter.SetPlayStatus("Источник избранного недоступен");
				return;
			}

			bool isFavorite = _trackFavoriteService.IsTrackFavorite(sourceId, trackId);
			if (isFavorite)
			{
				await _trackFavoriteService.RemoveFromFavorites(sourceId, trackId);
				_playStatusPresenter.SetPlayStatus("Удалён из избранного");
			}
			else
			{
				await _trackFavoriteService.AddToFavorites(sourceId, trackId);
				_playStatusPresenter.SetPlayStatus("Добавлен в избранное");
			}

			_playStatusPresenter.SetCurrentTrack(trackId, _trackSourceDetector.GetSourceId(trackId));
		}
		catch (Exception ex)
		{
			ex.Handle();
			_playStatusPresenter.SetPlayStatus("Не удалось обновить избранное");
		}
	}

	public async void RunSearchAsync(string source, string query, SearchEntityKind kind = SearchEntityKind.Tracks)
	{
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

				foreach (var track in tracks)
					await _trackInfoProvider.SaveAsync(track);
				_trackRepositoryCache.ReplaceYandexSearchTracks(tracks);
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

			var playlist = new Playlist(
				isYandex ? "Поиск по ЯМ" : "Локальный поиск",
				isYandex ? PlaylistType.YandexSearch : PlaylistType.LocalSearch)
			{
				Description = $"Результаты поиска: {query}",
				TrackCount = tracks.Count,
				SourceId = source,
				ParentTag = isYandex ? SourceIds.Yandex : SourceIds.Local
			};

			await _trackRepository.SetPlaylist(playlist);
			await _tracksPresenter.LoadTracksFor(playlist);
			_window.Title = $"{playlist.PlaylistName} : {playlist.Description}";
			_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		}
		catch (Exception ex) { ex.Handle(); }
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

	public async void ShowYandexSearchDialog()
	{
		try
		{
			_yandexSearchPresenter.ShowYandexSearchDialog();
			if (_yandexSearchPresenter.WasCancelled()) return;

			var selectedTracks = _yandexSearchPresenter.GetSelectedTracks();
			if (selectedTracks.Count == 0) return;

			foreach (var track in selectedTracks)
				await _trackInfoProvider.SaveAsync(track);

			_trackRepositoryCache.ReplaceYandexSearchTracks(selectedTracks);
			var playlist = new Playlist("Поиск по ЯМ", PlaylistType.YandexSearch)
			{
				Description = "Результаты поиска по Яндекс.Музыке",
				TrackCount = selectedTracks.Count,
				SourceId = SourceIds.Yandex,
				ParentTag = SourceIds.Yandex
			};
			await _trackRepository.SetPlaylist(playlist);
			await _tracksPresenter.LoadTracksFor(playlist);
			_window.Title = $"{playlist.PlaylistName} : {playlist.Description}";
			_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		}
		catch (Exception ex) { ex.Handle(); }
	}

	public async void ShowLocalSearchDialog()
	{
		try
		{
			_localSearchPresenter.ShowLocalSearchDialog();
			if (_localSearchPresenter.WasCancelled()) return;

			var selectedTracks = _localSearchPresenter.GetSelectedTracks();
			if (selectedTracks.Count == 0) return;

			_trackRepositoryCache.ReplaceLocalSearchTracks(selectedTracks);
			var playlist = new Playlist("Локальный поиск", PlaylistType.LocalSearch)
			{
				Description = "Результаты локального поиска",
				TrackCount = selectedTracks.Count,
				SourceId = SourceIds.Local,
				ParentTag = SourceIds.Local
			};
			await _trackRepository.SetPlaylist(playlist);
			await _tracksPresenter.LoadTracksFor(playlist);
			_window.Title = $"{playlist.PlaylistName} : {playlist.Description}";
			_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		}
		catch (Exception ex) { ex.Handle(); }
	}

	// ── Radio / Wave ──────────────────────────────────────────────────────

	public async void ShowMyWave()
	{
		var playlist = await _myWavePresenter.StartMyWaveAsync();
		if (playlist is null) return;
		_playbackPresenter.SetPlaylistType(PlaylistType.MyWave);
		_window.Title = $"{playlist.PlaylistName} : {playlist.Description}";
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		_myWaveWindowPresenter.ShowWindow(playlist);
	}

	public async void ShowMyWaveByTrack()
	{
		var trackId = _playbackQueue.CurrentTrackId;
		if (trackId == null)
		{
			_playStatusPresenter.SetPlayStatus("Сначала начните воспроизведение трека");
			return;
		}

		var playlist = await _myWavePresenter.StartMyWaveFromTrackAsync(trackId);
		if (playlist is null) return;
		_playbackPresenter.SetPlaylistType(PlaylistType.MyWave);
		_window.Title = $"{playlist.PlaylistName} : {playlist.Description}";
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		_myWaveWindowPresenter.ShowWindow(playlist);
	}

	// ── Local library ─────────────────────────────────────────────────────

	public void ShowAddLocalFolderDialog()
	{
		var od = new OpenDialog("Добавить папку", "Выберите папку с музыкой")
		{
			CanChooseDirectories = true,
			CanChooseFiles = false
		};
		Application.Run(od);

		if (!od.Canceled && od.FilePath != null)
		{
			string path = od.FilePath.ToString()!;
			_ = Task.Run(async () =>
			{
				try
				{
					var libraryService = ServicesProvider.Ioc.Resolve<ILocalLibraryService>();
					await libraryService.AddFolderAsync(path);
					Application.MainLoop.Invoke(RefreshPlaylistTree);
				}
				catch (Exception ex) { Application.MainLoop.Invoke(() => ex.Handle()); }
			});
		}
	}

	public async void ShowLocalFolderManagerDialog()
	{
		var presenter = ServicesProvider.Ioc.Resolve<ILocalFolderManagerPresenter>();
		presenter.OnLibraryChanged += RefreshPlaylistTree;
		try { await presenter.ShowAsync(); }
		finally { presenter.OnLibraryChanged -= RefreshPlaylistTree; }
	}

	public void ScanLocalLibrary()
	{
		_ = Task.Run(async () =>
		{
			try
			{
				var libraryService = ServicesProvider.Ioc.Resolve<ILocalLibraryService>();
				int count = await libraryService.ScanAllFoldersAsync();
				Application.MainLoop.Invoke(() =>
				{
					RefreshPlaylistTree();
					MessageBox.Query("Сканирование завершено", $"Найдено треков: {count}", "OK");
				});
			}
			catch (Exception ex) { Application.MainLoop.Invoke(() => ex.Handle()); }
		});
	}

	public void RefreshPlaylistTree()
		=> _playlistsPresenter.LoadPlaylistTree();

	// ── Menu actions forwarded ────────────────────────────────────────────

	public void ShowEqualizer() => _equalizerPresenter.ShowEqualizerDialog();
	public void ShowDbStats() => _dbStatsPresenter.ShowStatisticsDialog();
	public void ShowNowPlaying() => _nowPlayingPresenter.ShowNowPlaying();
	public void ShowLargeTrackInfo() => _largeTrackInfoPresenter.ShowLargeTrackInfo();

	public void StopApplication()
	{
		_startupCts?.Cancel();
		int result = MessageBox.Query("Выход", "Вы уверены, что хотите выйти?", "Да", "Нет");
		if (result == 0)
		{
			_audioPlayer.Free();
			Application.RequestStop();
			Console.Clear();
		}
	}

	public void Dispose()
	{
		_startupCts?.Cancel();
		_startupCts?.Dispose();
		if (_onTrackChangedHandler is not null)
		{
			_eventBus.Unsubscribe(_onTrackChangedHandler);
			_onTrackChangedHandler = null;
		}
		_audioPlayer.OnPreloadRequested -= OnPreloadNextTrack;
	}
}
