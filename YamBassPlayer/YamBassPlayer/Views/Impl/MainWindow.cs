using Terminal.Gui;
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
	private readonly SplashScreenView? _splashScreen;

	public MainWindow(
		IPlaylistsPresenter playlistsPresenter,
		ITracksPresenter tracksPresenter,
		IPlayStatusPresenter playStatusPresenter,
		IEqualizerPresenter equalizerPresenter,
		ILocalSearchPresenter localSearchPresenter,
		IYandexSearchPresenter yandexSearchPresenter,
		IDatabaseStatisticsPresenter dbStatsPresenter,
		ITrackFileProvider trackFileProvider,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		ITrackRepository trackRepository,
		IListenTimer listenTimer,
		IAudioPlayer audioPlayer,
		PlayStatusView playStatusView,
		PlaylistsView playlistsView,
		TracksTileView tracksView)
		: base(YamBassPlayerTitle)
	{
		_playlistsPresenter = playlistsPresenter;
		_tracksPresenter = tracksPresenter;
		_playStatusPresenter = playStatusPresenter;
		_equalizerPresenter = equalizerPresenter;
		_localSearchPresenter = localSearchPresenter;
		_yandexSearchPresenter = yandexSearchPresenter;
		_dbStatsPresenter = dbStatsPresenter;
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
		playlistsView.Height = 25;

		tracksView.X = Pos.Right(playlistsView);
		tracksView.Width = Dim.Fill();
		tracksView.Height = Dim.Fill(5);

		SpectrumView spectrum = new SpectrumView()
		{
			X = 0,
			Y = Pos.Top(playStatusView) - 15,
			Width = 25,
			Height = 15,
			Bars = 25
		};

		Add(playlistsView, spectrum, tracksView, playStatusView);

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
		
		_audioPlayer.OnPreloadRequested += OnPreloadNextTrack;
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
			_listenTimer.OnTrackStart(trackId);
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
				new MenuItem("Статистика БД", "", () => _dbStatsPresenter.ShowStatisticsDialog())
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
				}
			}
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
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
				}
			}
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}
}