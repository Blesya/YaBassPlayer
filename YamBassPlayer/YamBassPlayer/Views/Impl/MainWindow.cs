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
	private readonly ITrackFileProvider _trackFileProvider;
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
	private readonly IRecommendationGraphPresenter _recommendationGraphPresenter;
	private readonly SplashScreenView? _splashScreen;
	private PlaylistType _currentPlaylistType = PlaylistType.Favorite;

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
		IRecommendationGraphPresenter recommendationGraphPresenter,
		ITrackInfoPanelPresenter trackInfoPanelPresenter,
		ITrackFileProvider trackFileProvider,
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
		_recommendationGraphPresenter = recommendationGraphPresenter;
		_trackFileProvider = trackFileProvider;
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

		SpectrumView spectrum = new SpectrumView(bars: 29)
		{
			X = 0,
			Y = Pos.Top(playStatusView) - 15,
			Width = 29,
			Height = 15
		};

		Add(playlistsView, spectrum, tracksView, trackInfoPanelView, playStatusView);

		_playbackQueue.OnTrackChanged += OnTrackForPlaySelected;

		_playStatusPresenter.OnStopClicked += _audioPlayer.Stop;
		_playStatusPresenter.OnPlayClicked += () =>
		{
			if (_audioPlayer.IsPlayed)
			{
				_audioPlayer.Pause();
				return;
			}

			_audioPlayer.Resume();
		};
		_playStatusPresenter.OnPrevClicked += _playbackQueue.Previous;
		_playStatusPresenter.OnNextClicked += _playbackQueue.Next;

		_playlistsPresenter.PlaylistChosen += OnPlaylistChosen;
		_tracksPresenter.OnTrackChosen += track => _trackInfoPanelPresenter.OnTrackSelected(track);
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			float[] fft = _audioPlayer.ChannelGetData();
			spectrum.SetFftData(fft);

			return true;
		});

		_splashScreen = new SplashScreenView();
		Application.Top.Add(_splashScreen);
		_playlistsPresenter.PlaylistChosen += PlaylistsPresenterOnPlaylistChosen;

		_playStatusPresenter.OnStopClicked += () => _listenTimer.OnTrackStopOrChange();
		_playStatusPresenter.OnQueueClicked += ShowCurrentQueue;
		
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
		};
	}

	private async void OnTrackForPlaySelected(string trackId)
	{
		try
		{
			Track track = await _trackInfoProvider.GetTrackInfoById(trackId);

			_playStatusPresenter.SetTitle($"Загружается трек: {track.Artist} - {track.Title}");
			string filePath = await _trackFileProvider.DownloadTrackAsync(trackId);
			if (string.IsNullOrWhiteSpace(filePath))
				return;

			_playStatusPresenter.SetPlayStatus($"Сейчас играет: {track.Artist} - {track.Title}");
			Console.Title = $"{track.Artist} - {track.Title}";
			_audioPlayer.Play(filePath);
			var source = _currentPlaylistType == PlaylistType.OnSameWave
				? ListenSource.OnSameWave
				: ListenSource.Regular;
			_listenTimer.OnTrackStart(trackId, source);
			_playStatusPresenter.SetCurrentTrackId(trackId);
		}
		catch (Exception exception)
		{
			exception.Handle();
		}
		finally
		{
			_playStatusPresenter.SetTitle("Управление воспроизведением");
		}
	}

	private async void OnPlaylistChosen(Playlist playlist)
	{
		_currentPlaylistType = playlist.Type;
		await _tracksPresenter.LoadTracksFor(playlist);
		Title = $"{playlist.PlaylistName} : {playlist.Description}";
	}

	private async void OnPreloadNextTrack(object? sender, EventArgs e)
	{
		try
		{
			var nextTrackId = _playbackQueue.PeekNextTrackId;
			if (nextTrackId == null)
				return;

			if (!_trackFileProvider.IsTrackDownloaded(nextTrackId))
			{
				Track nextTrack = await _trackInfoProvider.GetTrackInfoById(nextTrackId);
				_playStatusPresenter.SetTitle($"Предзагрузка: {nextTrack.Artist} - {nextTrack.Title}");
				await _trackFileProvider.DownloadTrackAsync(nextTrackId);
			}
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
		finally
		{
			_playStatusPresenter.SetTitle("Управление воспроизведением");
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
				new MenuItem("На одной волне [F6]", "", ShowOnSameWave),
				new MenuItem("Граф рекомендаций [F7]", "", () => _recommendationGraphPresenter.ShowRecommendationGraph()),
				new MenuItem("Статистика БД", "", () => _dbStatsPresenter.ShowStatisticsDialog())
			}),
			new MenuBarItem("Вид", new[]
			{
				new MenuItem("Визуализация [F5]", "", () => _nowPlayingPresenter.ShowNowPlaying()),
				new MenuItem("Крупное инфо [F8]", "", () => _largeTrackInfoPresenter.ShowLargeTrackInfo())
			})
		});

		return menuBar;
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
		_currentPlaylistType = PlaylistType.OnSameWave;
		Title = $"{playlist.PlaylistName} : {playlist.Description}";
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
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