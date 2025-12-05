using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;


namespace YamBassPlayer.Views
{
	public sealed class MainWindow : Window
	{
		private const string YamBassPlayerTitle = "YamBassPlayer";
		private static readonly string TracksFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tracks");

		private readonly PlaylistsPresenter _playlistsPresenter;
		private readonly TracksPresenter _tracksPresenter;
		private readonly PlayStatusPresenter _playStatusPresenter;
		private readonly TrackFileProvider _trackFileProvider;
        private readonly PlaybackQueue _playbackQueue;
        private readonly TrackInfoProvider _trackInfoProvider;

        public MainWindow(AuthService authService)
			: base(YamBassPlayerTitle)
		{
			MenuBar menuBar = CreateMenuBar();
			Application.Top.Add(menuBar);

			var playStatusView = new PlayStatusView
			{
				X = 0,
				Y = Pos.AnchorEnd(5),
				Width = Dim.Fill(),
				Height = 5
			};

			var playlistsView = new PlaylistsView
			{
				X = 0,
				Width = 25,
				Height = 25
			};

			var tracksView = new TracksView
			{
				X = Pos.Right(playlistsView),
				Width = Dim.Fill(),
				Height = Dim.Fill(5)
			};

			SpectrumView spectrum = new SpectrumView()
			{
				X = 0,
				Y = Pos.Top(playStatusView) - 15,
				Width = 25,
				Height = 15,
				Bars = 25
			};

			Add(playlistsView, spectrum, tracksView, playStatusView);

            _trackFileProvider = new TrackFileProvider(authService.Api, authService.Storage, TracksFolder);
            _playbackQueue = new PlaybackQueue();
            _playbackQueue.OnTrackChanged += OnTrackForPlaySelected;

            _trackInfoProvider = new TrackInfoProvider(authService.Api, authService.Storage);

            ITrackRepository trackRepository = new TrackRepository(authService.Api, authService.Storage, TracksFolder);
            _playlistsPresenter = new PlaylistsPresenter(playlistsView, trackRepository);
            _tracksPresenter = new TracksPresenter(tracksView, _trackFileProvider, trackRepository, _playbackQueue);

            _playStatusPresenter = new PlayStatusPresenter(playStatusView);

			_playStatusPresenter.OnStopClicked += AudioPlayer.Stop;
            _playStatusPresenter.OnPlayClicked += () =>
            {
                if (AudioPlayer.IsPlayed)
                {
                    AudioPlayer.Pause();
                    return;
                }

                AudioPlayer.Resume();
            };
            _playStatusPresenter.OnPrevClicked += _playbackQueue.Previous;
            _playStatusPresenter.OnNextClicked += _playbackQueue.Next;

			_playlistsPresenter.PlaylistChosen += OnPlaylistChosen;
			Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
			{
				float[] fft = AudioPlayer.ChannelGetData();
				spectrum.SetFftData(fft);

				return true;
			});
		}

        private async void OnTrackForPlaySelected(string trackId)
        {
            try
            {
                Track track = await _trackInfoProvider.GetTrackInfoById(trackId);

                _playStatusPresenter.SetTilte($"Загружается трек: {track.Artist} - {track.Title}");
                string filePath = await _trackFileProvider.DownloadTrackAsync(trackId);
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                _playStatusPresenter.SetPlayStatus($"Сейчас играет: {track.Artist} - {track.Title}");
                Console.Title = $"{track.Artist} - {track.Title}";
                AudioPlayer.Play(filePath);
            }
            catch (Exception exception)
			{
				exception.Handle();
			}
			finally
			{
				_playStatusPresenter.SetTilte("Управление воспроизведением");
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
				})
			});

			return menuBar;
		}

		private static void Stop()
		{
			int result = MessageBox.Query("Выход", "Вы уверены, что хотите выйти?", "Да", "Нет");
			if (result == 0)
			{
				AudioPlayer.Free();
				Application.RequestStop();
				Console.Clear();
			}
		}
	}
}