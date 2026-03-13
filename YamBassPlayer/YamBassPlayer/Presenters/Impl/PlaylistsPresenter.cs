using YamBassPlayer.Extensions;
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
		try
		{
			var roots = await _trackRepository.GetPlaylistTree();
			_roots = roots.ToList();
			_view.SetPlaylistTree(_roots);

			var firstPlaylist = _roots.FirstOrDefault(r => r.Playlist != null)?.Playlist;
			if (firstPlaylist != null)
			{
				_view.MarkAsPlaying(firstPlaylist);
				PlaylistChosen?.Invoke(firstPlaylist);
			}
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	public void NotifyTransientPlaylistActive(Playlist playlist)
	{
		_view.AddOrUpdateTransientPlaylist(playlist);
		_view.MarkAsPlaying(playlist);
	}

	private void OnPlaylistSelected(Playlist playlist)
	{
		_view.MarkAsPlaying(playlist);
		PlaylistChosen?.Invoke(playlist);
	}
}