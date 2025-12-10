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
	private readonly IListenTimer _listenTimer;
	private readonly IAudioPlayer _audioPlayer;
	private readonly IEqualizerPresenter _equalizerPresenter;
	private readonly SplashScreenView? _splashScreen;

	public MainWindow(
		IPlaylistsPresenter playlistsPresenter,
		ITracksPresenter tracksPresenter,
		IPlayStatusPresenter playStatusPresenter,
		IEqualizerPresenter equalizerPresenter,
		ITrackFileProvider trackFileProvider,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		IListenTimer listenTimer,
		IAudioPlayer audioPlayer,
		PlayStatusView playStatusView,
		PlaylistsView playlistsView,
		TracksView tracksView)
		: base(YamBassPlayerTitle)
	{
		_playlistsPresenter = playlistsPresenter;
		_tracksPresenter = tracksPresenter;
		_playStatusPresenter = playStatusPresenter;
		_equalizerPresenter = equalizerPresenter;
		_trackFileProvider = trackFileProvider;
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_listenTimer = listenTimer;
		_audioPlayer = audioPlayer;

		MenuBar menuBar = CreateMenuBar();
		Application.Top.Add(menuBar);

		playStatusView.X = 0;
		playStatusView.Y = Pos.AnchorEnd(5);
		playStatusView.Width = Dim.Fill();
		playStatusView.Height = 5;

		playlistsView.X = 0;
		playlistsView.Width = 25;
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
				new MenuItem("Матрица", "", () => Themes.ApplyMatrixTheme()),
				new MenuItem("Киберпанк", "", () => Themes.ApplyCyberpunkTheme()),
				new MenuItem("Спокойная", "", () => Themes.ApplyNordTheme()),
				new MenuItem("По умолчанию", "", () => Themes.RestoreDefaultTheme())
			}),
			new MenuBarItem("Аудио", new[]
			{
				new MenuItem("Эквалайзер", "", () => _equalizerPresenter.ShowEqualizerDialog())
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
}