using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters
{
	public class PlaylistsPresenter
	{
		private readonly PlaylistsView _view;
		private readonly IPlaylistsService _playlistsService;

		private List<Playlist> _playlists = new();
		public event Action<Playlist>? PlaylistChosen;

		public PlaylistsPresenter(PlaylistsView view, IPlaylistsService playlistsService)
		{
			_view = view;
			_playlistsService = playlistsService;

			_view.PlaylistSelected += OnPlaylistSelected;

			LoadPlaylists();
		}

		private async void LoadPlaylists()
		{
			var asd = await _playlistsService.GetPlaylists();
			_playlists = asd.ToList();
			_view.SetPlaylists(_playlists);
		}

		private void OnPlaylistSelected(int index)
		{
			if (index < 0 || index >= _playlists.Count)
				return;

			PlaylistChosen?.Invoke(_playlists[index]);
		}
	}

}
