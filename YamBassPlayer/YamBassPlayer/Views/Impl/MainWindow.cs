using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;

namespace YamBassPlayer.Views.Impl;

public sealed class MainWindow : Window
{
	private const string YamBassPlayerTitle = "YamBassPlayer";

	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IPlaybackCoordinator _playbackCoordinator;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ITrackRepository _trackRepository;
	private readonly IListenTimer _listenTimer;
	private readonly IAudioPlayer _audioPlayer;
	private readonly IEqualizerPresenter _equalizerPresenter;
	private readonly ILocalSearchPresenter _localSearchPresenter;
	private readonly IYandexSearchPresenter _yandexSearchPresenter;
	private readonly IDatabaseStatisticsPresenter _dbStatsPresenter;
	private readonly INowPlayingPresenter _nowPlayingPresenter;
	private readonly ILargeTrackInfoPresenter _largeTrackInfoPresenter;
	private readonly ITrackInfoPanelPresenter _trackInfoPanelPresenter;
	private readonly IOnSameWavePresenter _onSameWavePresenter;
	private readonly IMyWavePresenter _myWavePresenter;
	private readonly IMyWaveWindowPresenter _myWaveWindowPresenter;
	private readonly IRecommendationGraphPresenter _recommendationGraphPresenter;
	private readonly SplashScreenView? _splashScreen;
	private SpectrumView _spectrum = null!;
	private Button _spectrumModeButton = null!;

	public MainWindow(
		IPlaylistsPresenter playlistsPresenter,
		ITracksPresenter tracksPresenter,
		IPlayStatusPresenter playStatusPresenter,
		IEqualizerPresenter equalizerPresenter,
		ILocalSearchPresenter localSearchPresenter,
		IYandexSearchPresenter yandexSearchPresenter,
		IDatabaseStatisticsPresenter dbStatsPresenter,
		INowPlayingPresenter nowPlayingPresenter,
		ILargeTrackInfoPresenter largeTrackInfoPresenter,
		IOnSameWavePresenter onSameWavePresenter,
		IMyWavePresenter myWavePresenter,
		IMyWaveWindowPresenter myWaveWindowPresenter,
		IRecommendationGraphPresenter recommendationGraphPresenter,
		ITrackInfoPanelPresenter trackInfoPanelPresenter,
		IPlaybackCoordinator playbackCoordinator,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		ITrackRepository trackRepository,
		IListenTimer listenTimer,
		IAudioPlayer audioPlayer,
		PlayStatusView playStatusView,
		PlaylistsView playlistsView,
		TracksTileView tracksView,
		TrackInfoPanelView trackInfoPanelView)
		: base(YamBassPlayerTitle)
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
		_trackInfoPanelPresenter = trackInfoPanelPresenter;
		_onSameWavePresenter = onSameWavePresenter;
		_myWavePresenter = myWavePresenter;
		_myWaveWindowPresenter = myWaveWindowPresenter;
		_recommendationGraphPresenter = recommendationGraphPresenter;
		_playbackCoordinator = playbackCoordinator;
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_trackRepository = trackRepository;
		_listenTimer = listenTimer;
		_audioPlayer = audioPlayer;

		MenuBar menuBar = CreateMenuBar();
		Application.Top.Add(menuBar);

		playStatusView.X = 0;
		playStatusView.Y = Pos.AnchorEnd(5);
		playStatusView.Width = Dim.Fill();
		playStatusView.Height = 5;

		playlistsView.X = 0;
		playlistsView.Width = 30;
		playlistsView.Height = Dim.Fill(5);

		const int panelWidth = 38;

		tracksView.X = Pos.Right(playlistsView);
		tracksView.Width = Dim.Fill(panelWidth);
		tracksView.Height = Dim.Fill(5);

		trackInfoPanelView.X = Pos.Right(tracksView);
		trackInfoPanelView.Y = 0;
		trackInfoPanelView.Width = panelWidth;
		trackInfoPanelView.Height = Dim.Fill(5);

		_spectrum = new SpectrumView(bars: 29)
		{
			X = 0,
			Y = Pos.Top(playStatusView) - 15,
			Width = 29,
			Height = 14
		};

		_spectrumModeButton = new Button
		{
			X = 0,
			Y = Pos.Top(playStatusView) - 1,
			Width = 29,
			Text = "≋ FFT"
		};
		_spectrumModeButton.Clicked += ToggleSpectrumMode;

		Add(playlistsView, _spectrum, _spectrumModeButton, tracksView, trackInfoPanelView, playStatusView);

		_playbackQueue.OnTrackChanged += OnTrackForPlaySelected;

		_playStatusPresenter.OnStopClicked += _audioPlayer.Stop;
		_playStatusPresenter.OnPlayClicked += () =>
		{
			if (_audioPlayer.IsPlayed)
			{
				_audioPlayer.Pause();
				_listenTimer.OnPause();
				return;
			}

			_audioPlayer.Resume();
			_listenTimer.OnResume();
		};
		_playStatusPresenter.OnPrevClicked += _playbackQueue.Previous;
		_playStatusPresenter.OnNextClicked += () =>
		{
			_playbackCoordinator.MarkMyWaveSkipPending();
			_playbackQueue.Next();
		};

		_playlistsPresenter.PlaylistChosen += OnPlaylistChosen;
		_tracksPresenter.OnTrackChosen += track => _trackInfoPanelPresenter.OnTrackSelected(track);
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			if (_spectrum.Mode == YamBassPlayer.Enums.SpectrumMode.Oscilloscope)
			{
				_spectrum.SetWaveformData(_audioPlayer.GetWaveformData(512));
			}
			else
			{
				_spectrum.SetFftData(_audioPlayer.ChannelGetData());
			}

			return true;
		});

		_splashScreen = new SplashScreenView();
		Application.Top.Add(_splashScreen);
		_playlistsPresenter.PlaylistChosen += PlaylistsPresenterOnPlaylistChosen;

		_playStatusPresenter.OnStopClicked += () => _listenTimer.OnTrackStopOrChange();
		_playStatusPresenter.OnQueueClicked += ShowCurrentQueue;
		_playStatusPresenter.OnPlaybackModeToggled += OnPlaybackModeToggled;
		
		_audioPlayer.OnPreloadRequested += OnPreloadNextTrack;

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.F5)
			{
				_nowPlayingPresenter.ShowNowPlaying();
				e.Handled = true;
			}

			if (e.KeyEvent.Key == Key.F6)
			{
				ShowOnSameWave();
				e.Handled = true;
			}

			if (e.KeyEvent.Key == Key.F7)
			{
				_recommendationGraphPresenter.ShowRecommendationGraph();
				e.Handled = true;
			}

			if (e.KeyEvent.Key == Key.F8)
			{
				_largeTrackInfoPresenter.ShowLargeTrackInfo();
				e.Handled = true;
			}

			if (e.KeyEvent.Key == Key.F9)
			{
				ShowMyWave();
				e.Handled = true;
			}
		};
	}

	private async void OnTrackForPlaySelected(string trackId)
	{
		try
		{
			await _playbackCoordinator.PlaySelectedTrackAsync(trackId);
		}
		catch (Exception exception)
		{
			exception.Handle();
		}
	}

	private async void OnPlaylistChosen(Playlist playlist)
	{
		_playbackCoordinator.SetPlaylistType(playlist.Type);
		await _tracksPresenter.LoadTracksFor(playlist);
		Title = $"{playlist.PlaylistName} : {playlist.Description}";
	}

	private async void OnPreloadNextTrack(object? sender, EventArgs e)
	{
		try
		{
			await _playbackCoordinator.PreloadNextTrackAsync();
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	private MenuBar CreateMenuBar()
	{
		var menuBar = new MenuBar(new[]
		{
			new MenuBarItem("Файл", new[]
			{
				new MenuItem("Выход", "[Выход из программы]",
					Stop)
			}),
			new MenuBarItem("Темы", new[]
			{
				new MenuItem("Тёмная", "", () => Themes.ApplyDarkTheme()),
				new MenuItem("Светлая", "", () => Themes.ApplyLightTheme()),
				new MenuItem("Белая", "", () => Themes.ApplyWhiteTheme()),
				new MenuItem("Матрица", "", () => Themes.ApplyMatrixTheme()),
				new MenuItem("Киберпанк", "", () => Themes.ApplyCyberpunkTheme()),
				new MenuItem("Спокойная", "", () => Themes.ApplyNordTheme()),
				new MenuItem("По умолчанию", "", () => Themes.RestoreDefaultTheme())
			}),
			new MenuBarItem("Аудио", new[]
			{
				new MenuItem("Эквалайзер", "", () => _equalizerPresenter.ShowEqualizerDialog())
			}),
			new MenuBarItem("Инструменты", new[]
			{
				new MenuItem("Локальный поиск", "", ShowLocalSearchDialog),
				new MenuItem("Поиск по ЯМ", "", ShowYandexSearchDialog),
				new MenuItem("Моя волна [F9]", "", ShowMyWave),
				new MenuItem("Моя волна по треку", "", ShowMyWaveByTrack),
				new MenuItem("На одной волне [F6]", "", ShowOnSameWave),
				new MenuItem("Граф рекомендаций [F7]", "", () => _recommendationGraphPresenter.ShowRecommendationGraph()),
				new MenuItem("Статистика БД", "", () => _dbStatsPresenter.ShowStatisticsDialog())
			}),
			new MenuBarItem("Вид", new[]
			{
				new MenuItem("Визуализация [F5]", "", () => _nowPlayingPresenter.ShowNowPlaying()),
				new MenuItem("Крупное инфо [F8]", "", () => _largeTrackInfoPresenter.ShowLargeTrackInfo()),
new MenuItem("≋ Спектр: FFT / Осциллограмм", "", ToggleSpectrumMode)
			})
		});

		return menuBar;
	}

	private void ToggleSpectrumMode()
	{
		_spectrum.Mode = _spectrum.Mode == YamBassPlayer.Enums.SpectrumMode.Bars
			? YamBassPlayer.Enums.SpectrumMode.Oscilloscope
			: YamBassPlayer.Enums.SpectrumMode.Bars;
		_spectrumModeButton.Text = _spectrum.Mode == YamBassPlayer.Enums.SpectrumMode.Oscilloscope
			? "〜 Осц."
			: "≋ FFT";
	}

	private void Stop()
	{
		int result = MessageBox.Query("Выход", "Вы уверены, что хотите выйти?", "Да", "Нет");
		if (result == 0)
		{
			_audioPlayer.Free();
			Application.RequestStop();
			Console.Clear();
		}
	}

	private void OnPlaybackModeToggled()
	{
		_playbackQueue.Mode = _playbackQueue.Mode == PlaybackMode.Shuffle
			? PlaybackMode.Sequential
			: PlaybackMode.Shuffle;
		_playStatusPresenter.SetPlaybackMode(_playbackQueue.Mode);
	}

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

			_trackRepository.UpdateQueueCache(trackIds);

			var queuePlaylist = new Playlist("Текущая очередь", Enums.PlaylistType.Queue)
			{
				Description = "Текущая очередь воспроизведения",
				TrackCount = trackIds.Count
			};

			await _tracksPresenter.LoadTracksFor(queuePlaylist);
			Title = $"{queuePlaylist.PlaylistName} : {queuePlaylist.Description}";
			_playlistsPresenter.NotifyTransientPlaylistActive(queuePlaylist);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	private void PlaylistsPresenterOnPlaylistChosen(Playlist obj)
	{
		Application.Top.Remove(_splashScreen);
		_playlistsPresenter.PlaylistChosen -= PlaylistsPresenterOnPlaylistChosen;
	}

	private async void ShowYandexSearchDialog()
	{
		try
		{
			_yandexSearchPresenter.ShowYandexSearchDialog();

			if (!_yandexSearchPresenter.WasCancelled())
			{
				var selectedTracks = _yandexSearchPresenter.GetSelectedTracks();
				if (selectedTracks.Count > 0)
				{
					foreach (var track in selectedTracks)
					{
						await _trackInfoProvider.SaveAsync(track);
					}

					_trackRepository.UpdateYandexSearchCache(selectedTracks);

					var yandexSearchPlaylist = new Playlist("Поиск по ЯМ", Enums.PlaylistType.YandexSearch)
					{
						Description = "Результаты поиска по Яндекс.Музыке",
						TrackCount = selectedTracks.Count
					};

					await _trackRepository.SetPlaylist(yandexSearchPlaylist);
					await _tracksPresenter.LoadTracksFor(yandexSearchPlaylist);
					Title = $"{yandexSearchPlaylist.PlaylistName} : {yandexSearchPlaylist.Description}";
					_playlistsPresenter.NotifyTransientPlaylistActive(yandexSearchPlaylist);
				}
			}
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	private async void ShowOnSameWave()
	{
		var playlist = await _onSameWavePresenter.ShowOnSameWaveAsync();
		if (playlist is null) return;
		_playbackCoordinator.SetPlaylistType(PlaylistType.OnSameWave);
		Title = $"{playlist.PlaylistName} : {playlist.Description}";
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
	}

	private async void ShowMyWave()
	{
		var playlist = await _myWavePresenter.StartMyWaveAsync();
		if (playlist is null) return;
		_playbackCoordinator.SetPlaylistType(PlaylistType.MyWave);
		Title = $"{playlist.PlaylistName} : {playlist.Description}";
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		_myWaveWindowPresenter.ShowWindow(playlist);
	}

	private async void ShowMyWaveByTrack()
	{
		var trackId = _playbackQueue.CurrentTrackId;
		if (trackId == null)
		{
			_playStatusPresenter.SetPlayStatus("Сначала начните воспроизведение трека");
			return;
		}

		var playlist = await _myWavePresenter.StartMyWaveFromTrackAsync(trackId);
		if (playlist is null) return;
		_playbackCoordinator.SetPlaylistType(PlaylistType.MyWave);
		Title = $"{playlist.PlaylistName} : {playlist.Description}";
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		_myWaveWindowPresenter.ShowWindow(playlist);
	}

	private async void ShowLocalSearchDialog()
	{
		try
		{
			_localSearchPresenter.ShowLocalSearchDialog();

			if (!_localSearchPresenter.WasCancelled())
			{
				var selectedTracks = _localSearchPresenter.GetSelectedTracks();
				if (selectedTracks.Count > 0)
				{
					_trackRepository.UpdateLocalSearchCache(selectedTracks);

					var localSearchPlaylist = new Playlist("Локальный поиск", Enums.PlaylistType.LocalSearch)
					{
						Description = "Результаты локального поиска",
						TrackCount = selectedTracks.Count
					};

					await _trackRepository.SetPlaylist(localSearchPlaylist);
					await _tracksPresenter.LoadTracksFor(localSearchPlaylist);
					Title = $"{localSearchPlaylist.PlaylistName} : {localSearchPlaylist.Description}";
					_playlistsPresenter.NotifyTransientPlaylistActive(localSearchPlaylist);
				}
			}
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}
}
