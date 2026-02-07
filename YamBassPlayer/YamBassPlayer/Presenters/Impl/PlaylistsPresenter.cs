using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class PlaylistsPresenter : IPlaylistsPresenter
{
	private readonly IPlaylistsView _view;
	private readonly ITrackRepository _trackRepository;

	private List<PlaylistTreeItem> _roots = new();
	public event Action<Playlist>? PlaylistChosen;

	public PlaylistsPresenter(IPlaylistsView view, ITrackRepository trackRepository)
	{
		_view = view;
		_trackRepository = trackRepository;

		_view.PlaylistSelected += OnPlaylistSelected;

		LoadPlaylists();
	}

	private async void LoadPlaylists()
	{
		var roots = await _trackRepository.GetPlaylistTree();
		_roots = roots.ToList();
		_view.SetPlaylistTree(_roots);

		var firstPlaylist = _roots.FirstOrDefault(r => r.Playlist != null)?.Playlist;
		if (firstPlaylist != null)
		{
			PlaylistChosen?.Invoke(firstPlaylist);
		}
	}

	private void OnPlaylistSelected(Playlist playlist)
	{
		PlaylistChosen?.Invoke(playlist);
	}
}