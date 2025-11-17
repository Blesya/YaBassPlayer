using NStack;
using Terminal.Gui;
using YamBassPlayer.Constants;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Views
{
	public sealed class MainWindow : Window
	{
		private const string YamBassPlayerTitle = "YamBassPlayer";
		private static string TracksFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tracks");

		private readonly PlaylistsPresenter _playlistsPresenter;
		private readonly TracksPresenter _tracksPresenter;
		private readonly TrackFileProvider _trackFileProvider;

		public MainWindow()
			: base(YamBassPlayerTitle)
		{
			MenuBar menuBar = CreateMenuBar();
			Add(menuBar);

			var playlistsView = new PlaylistsView
			{
				X = 0,
				Width = 25,
				Height = Dim.Fill()
			};

			var tracksView = new TracksView
			{
				X = Pos.Right(playlistsView),
				Width = Dim.Fill(),
				Height = Dim.Fill()
			};

			Add(playlistsView, tracksView);

			AuthStorage storage = new AuthStorage();
			YandexMusicApi api = new YandexMusicApi();
			api.User.AuthorizeAsync(storage, AuthConst.TOKEN);

			_trackFileProvider = new TrackFileProvider(api, storage, TracksFolder);

			IPlaylistsService playlistsService = new PlaylistsService(api, storage);
			_playlistsPresenter = new PlaylistsPresenter(playlistsView, playlistsService);

			ITracksService tracksService = new TracksService(api, storage);
			_tracksPresenter = new TracksPresenter(tracksView, tracksService);
			_tracksPresenter.OnTrackForPlaySelected += OnTrackForPlaySelected;

			_playlistsPresenter.PlaylistChosen += OnPlaylistChosen;
		}

		private async void OnTrackForPlaySelected(Track track)
		{
			string filePath = await _trackFileProvider.DownloadTrackAsync(track.Id);
			AudioPlayer.Play(filePath);
		}

		private void OnPlaylistChosen(Playlist playlist)
		{
			_tracksPresenter.LoadTracksFor(playlist);
			Title = $"{playlist.PlaylistName} : {playlist.Description}";
		}

		private MenuBar CreateMenuBar()
		{
			var menuBar = new MenuBar(new[]
			{
				new MenuBarItem("Файл", new[]
				{
					new MenuItem("Выход", "[Выход из программы]",
						() => { Application.RequestStop(); })
				})
			});

			return menuBar;
		}
	}
}