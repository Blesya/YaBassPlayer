using System.Threading;
using Serilog;
using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.UseCases;
using YamBassPlayer.Views;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Coordinates all presenter interactions, playback, and keyboard shortcuts.
/// Extracted from MainWindow to reduce its responsibility to pure UI composition.
/// Search/local-library/radio/favorite pipelines are delegated to use cases.
/// </summary>
public sealed class MainWindowCoordinator : IDisposable
{
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IPlaybackPresenter _playbackPresenter;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly INextTrackPredictor _nextTrackPredictor;
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
	private readonly ICommandInputView _commandInputView;
	private readonly SearchAndLoadPlaylistUseCase _searchAndLoadPlaylistUseCase;
	private readonly ToggleFavoriteUseCase _toggleFavoriteUseCase;
	private readonly ScanLibraryUseCase _scanLibraryUseCase;
	private readonly ShowMyWaveUseCase _showMyWaveUseCase;
	private CancellationTokenSource? _startupCts;
	private Window _window = null!; // Set later via SetWindow()
	private string? _currentTrackId;
	private string? _previousTrackId;
	private string? _previous2Id;

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
		ITrackInfoPanelPresenter trackInfoPanelPresenter,
		ICommandInputView commandInputView,
		IPlaybackPresenter playbackPresenter,
		IPlaybackQueue playbackQueue,
		ITrackRepository trackRepository,
		ITrackRepositoryCache trackRepositoryCache,
		INextTrackPredictor nextTrackPredictor,
		IListenTimer listenTimer,
		IAudioPlayer audioPlayer,
		IEventBus eventBus,
		SearchAndLoadPlaylistUseCase searchAndLoadPlaylistUseCase,
		ToggleFavoriteUseCase toggleFavoriteUseCase,
		ScanLibraryUseCase scanLibraryUseCase,
		ShowMyWaveUseCase showMyWaveUseCase)
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
		_commandInputView = commandInputView;
		_playbackPresenter = playbackPresenter;
		_playbackQueue = playbackQueue;
		_trackRepository = trackRepository;
		_trackRepositoryCache = trackRepositoryCache;
		_nextTrackPredictor = nextTrackPredictor;
		_listenTimer = listenTimer;
		_audioPlayer = audioPlayer;
		_eventBus = eventBus;
		_searchAndLoadPlaylistUseCase = searchAndLoadPlaylistUseCase;
		_toggleFavoriteUseCase = toggleFavoriteUseCase;
		_scanLibraryUseCase = scanLibraryUseCase;
		_showMyWaveUseCase = showMyWaveUseCase;
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

		_playbackQueue.OnTrackChanged += trackId =>
		{
			_previous2Id = _previousTrackId;
			_previousTrackId = _currentTrackId;
			_currentTrackId = trackId;
		};

		_playlistsPresenter.PlaylistChosen += _ =>
		{
			// Событие может прийти повторно (фоновое обновление дерева) —
			// сплеш снимаем только один раз.
			if (splashScreen.SuperView is not null)
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
			if (e.KeyEvent.Key == Key.F5)
			{
				Logging.LogUserAction("горячая клавиша F5 — визуализация");
				_nowPlayingPresenter.ShowNowPlaying();
				e.Handled = true;
			}
			if (e.KeyEvent.Key == Key.F8)
			{
				Logging.LogUserAction("горячая клавиша F8 — крупное инфо");
				_largeTrackInfoPresenter.ShowLargeTrackInfo();
				e.Handled = true;
			}
			if (e.KeyEvent.Key == Key.F9)
			{
				Logging.LogUserAction("горячая клавиша F9 — «Моя волна»");
				ShowMyWave();
				e.Handled = true;
			}
			if (e.KeyEvent.Key == (Key)(int)'~' || e.KeyEvent.Key == (Key)(int)'ё' || e.KeyEvent.Key == (Key)(int)'Ё')
			{
				Logging.LogUserAction("горячая клавиша «~» — фокус на командной строке");
				_commandInputView.FocusInput();
				e.Handled = true;
			}
		};
	}

	// ── Playback ──────────────────────────────────────────────────────────

	private void OnTrackForPlaySelected(string trackId)
		=> PlaySelectedTrackAsync(trackId).Forget();

	private async Task PlaySelectedTrackAsync(string trackId)
	{
		try { await _playbackPresenter.PlaySelectedTrackAsync(trackId); }
		catch (Exception ex) { ex.Handle(); }
	}

	private void OnPlaylistChosen(Playlist playlist)
		=> LoadPlaylistAsync(playlist).Forget();

	private async Task LoadPlaylistAsync(Playlist playlist)
	{
		Log.Information("Загрузка плейлиста: «{PlaylistName}» ({Type})", playlist.PlaylistName, playlist.Type);
		_playbackPresenter.SetPlaylistType(playlist.Type);
		await _tracksPresenter.LoadTracksFor(playlist);
		_window.Title = $"{playlist.PlaylistName} : {playlist.Description}";
	}

	private void OnPreloadNextTrack(object? sender, EventArgs e)
		=> PreloadNextTrackAsync().Forget();

	private async Task PreloadNextTrackAsync()
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
		Logging.LogUserAction(_playbackQueue.Mode == PlaybackMode.Shuffle
			? "режим перемешивания"
			: "последовательный режим");
	}

	// ── Общие обработчики воспроизведения (кнопки + командные интенты) ─────

	private void TogglePlayPause()
	{
		if (_audioPlayer.IsPlayed)
		{
			Logging.LogUserAction("пауза");
			_audioPlayer.Pause();
			_listenTimer.OnPause();
			return;
		}
		Logging.LogUserAction("воспроизведение");
		_audioPlayer.Resume();
		_listenTimer.OnResume();
	}

	private void ResumePlayback()
	{
		Logging.LogUserAction("воспроизведение (команда)");
		_audioPlayer.Resume();
		_listenTimer.OnResume();
	}

	private void PausePlayback()
	{
		Logging.LogUserAction("пауза (команда)");
		_audioPlayer.Pause();
		_listenTimer.OnPause();
	}

	private void StopPlayback()
	{
		Logging.LogUserAction("остановка воспроизведения");
		_audioPlayer.Stop();
		_listenTimer.OnTrackStopOrChange();
	}

	private void NextTrack()
	{
		Logging.LogUserAction("следующий трек");
		_playbackPresenter.MarkMyWaveSkipPending();
		_playbackQueue.Next();
	}

	private void PrevTrack()
	{
		Logging.LogUserAction("предыдущий трек");
		_playbackQueue.Previous();
	}

	private void RestartTrack()
	{
		Logging.LogUserAction("рестарт трека");
		_audioPlayer.SeekToPercent(0);
		_audioPlayer.Resume();
		_listenTimer.OnResume();
	}

	private void SeekTo(int percent)
	{
		Logging.LogUserAction($"перемотка на {percent}%");
		_audioPlayer.SeekToPercent(percent);
	}

	private void SetShuffle(bool shuffle)
	{
		Logging.LogUserAction(shuffle ? "режим перемешивания" : "последовательный режим");
		_playbackQueue.Mode = shuffle ? PlaybackMode.Shuffle : PlaybackMode.Sequential;
		_playStatusPresenter.SetPlaybackMode(_playbackQueue.Mode);
	}

	private void PlayTrackAt(int index)
	{
		var trackIds = _trackRepository.GetAllTrackIds();
		if (index < 0 || index >= trackIds.Count)
			return;

		Logging.LogUserAction($"воспроизведение трека №{index + 1}");
		_playbackQueue.SetQueue(trackIds, index);
	}

	// ── Queue ─────────────────────────────────────────────────────────────

	private void ShowCurrentQueue()
	{
		Logging.LogUserAction("просмотр очереди");
		ShowCurrentQueueAsync().Forget();
	}

	private async Task ShowCurrentQueueAsync()
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

	// ── Search / Favorites ────────────────────────────────────────────────

	public void ToggleFavoriteCommandAsync(string sourceId, string trackId)
	{
		Logging.LogUserAction($"переключение избранного ({sourceId})");
		_toggleFavoriteUseCase.ExecuteAsync(sourceId, trackId).Forget();
	}

	public void RunSearchAsync(string source, string query, SearchEntityKind kind = SearchEntityKind.Tracks)
	{
		Logging.LogUserAction($"поиск ({source}): «{query}»");
		_searchAndLoadPlaylistUseCase.SearchAndLoadAsync(source, query, kind, SetWindowTitle).Forget();
	}

	public void ShowYandexSearchDialog()
	{
		Logging.LogUserAction("поиск по Яндекс.Музыке");
		ShowYandexSearchDialogAsync().Forget();
	}

	private async Task ShowYandexSearchDialogAsync()
	{
		try
		{
			_yandexSearchPresenter.ShowYandexSearchDialog();
			if (_yandexSearchPresenter.WasCancelled()) return;

			var selectedTracks = _yandexSearchPresenter.GetSelectedTracks();
			if (selectedTracks.Count == 0) return;

			await _searchAndLoadPlaylistUseCase.LoadYandexSelectionAsync(selectedTracks, SetWindowTitle);
		}
		catch (Exception ex) { ex.Handle(); }
	}

	public void ShowLocalSearchDialog()
	{
		Logging.LogUserAction("локальный поиск");
		ShowLocalSearchDialogAsync().Forget();
	}

	private async Task ShowLocalSearchDialogAsync()
	{
		try
		{
			_localSearchPresenter.ShowLocalSearchDialog();
			if (_localSearchPresenter.WasCancelled()) return;

			var selectedTracks = _localSearchPresenter.GetSelectedTracks();
			if (selectedTracks.Count == 0) return;

			await _searchAndLoadPlaylistUseCase.LoadLocalSelectionAsync(selectedTracks, SetWindowTitle);
		}
		catch (Exception ex) { ex.Handle(); }
	}

	// ── Radio / Wave ──────────────────────────────────────────────────────

	public void ShowMyWave()
	{
		Logging.LogUserAction("запуск «Моей волны»");
		_showMyWaveUseCase.ShowAsync(SetWindowTitle).Forget();
	}

	public void ShowMyWaveByTrack()
	{
		Logging.LogUserAction("«Моя волна» по текущему треку");
		_showMyWaveUseCase.ShowByTrackAsync(SetWindowTitle).Forget();
	}

	// ── Local library ─────────────────────────────────────────────────────

	public void ShowAddLocalFolderDialog()
	{
		Logging.LogUserAction("добавление локальной папки");
		var od = new OpenDialog("Добавить папку", "Выберите папку с музыкой")
		{
			CanChooseDirectories = true,
			CanChooseFiles = false
		};
		Application.Run(od);

		if (!od.Canceled && od.FilePath != null)
		{
			string path = od.FilePath.ToString()!;
			Log.Information("Выбрана локальная папка: {Path}", path);
			Task.Run(() => _scanLibraryUseCase.AddFolderAsync(path)).Forget();
		}
	}

	public void ShowLocalFolderManagerDialog()
	{
		Logging.LogUserAction("управление локальными папками");
		_scanLibraryUseCase.ShowFolderManagerAsync().Forget();
	}

	public void ScanLocalLibrary()
	{
		Logging.LogUserAction("сканирование локальной библиотеки");
		Task.Run(async () =>
		{
			int? count = await _scanLibraryUseCase.ScanAllFoldersAsync();
			if (count is null) return;

			Application.MainLoop.Invoke(() =>
				MessageBox.Query("Сканирование завершено", $"Найдено треков: {count.Value}", "OK"));
		}).Forget();
	}

	public void RefreshPlaylistTree()
	{
		Logging.LogUserAction("обновление дерева плейлистов");
		_playlistsPresenter.LoadPlaylistTree();
	}

	// ── Menu actions forwarded ────────────────────────────────────────────

	public void RecommendNextTrack()
	{
		Logging.LogUserAction("рекомендация следующего трека");

		try
		{
			if (_currentTrackId == null)
			{
				_playStatusPresenter.SetPlayStatus("Сначала начните воспроизведение трека");
				return;
			}

			if (!_nextTrackPredictor.IsReady)
			{
				_playStatusPresenter.SetPlayStatus("Модель рекомендаций недоступна (нет файлов модели)");
				return;
			}

			var result = _nextTrackPredictor.GetNext(
				_previousTrackId ?? "",
				_currentTrackId,
				_previous2Id ?? "");
			if (!result.IsSuccess)
			{
				HelpDialog.Show("Рекомендация следующего трека", result.Message);
				return;
			}

			// Ставим рекомендованный трек текущим в очереди. SetQueue поднимет
			// OnTrackChanged → обновятся prev/current и запустится воспроизведение.
			var trackIds = _playbackQueue.TrackIds.ToList();
			if (!trackIds.Contains(result.TrackId!))
				trackIds.Add(result.TrackId!);
			int index = trackIds.IndexOf(result.TrackId!);
			_playbackQueue.SetQueue(trackIds, index);
		}
		catch (Exception ex)
		{
			ex.Handle();
			_playStatusPresenter.SetPlayStatus("Не удалось получить рекомендацию");
		}
	}

	public void ShowEqualizer()
	{
		Logging.LogUserAction("эквалайзер");
		_equalizerPresenter.ShowEqualizerDialog();
	}

	public void ShowDbStats()
	{
		Logging.LogUserAction("статистика базы данных");
		_dbStatsPresenter.ShowStatisticsDialog();
	}

	public void ShowNowPlaying()
	{
		Logging.LogUserAction("визуализация «Сейчас играет»");
		_nowPlayingPresenter.ShowNowPlaying();
	}

	public void ShowLargeTrackInfo()
	{
		Logging.LogUserAction("крупное инфо о треке");
		_largeTrackInfoPresenter.ShowLargeTrackInfo();
	}

	public void ShowAbout()
	{
		Logging.LogUserAction("о программе");
		AboutDialog.Show();
	}

	private void SetWindowTitle(string title) => _window.Title = title;

	public void StopApplication()
	{
		Logging.LogUserAction("выход из приложения");
		_startupCts?.Cancel();
		int result = MessageBox.Query("Выход", "Вы уверены, что хотите выйти?", "Да", "Нет");
		if (result == 0)
		{
			Log.Information("Выход подтверждён пользователем");
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
