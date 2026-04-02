using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class PlaylistsPresenter : IPlaylistsPresenter
{
	private readonly IPlaylistsView _view;
	private readonly ITrackRepository _trackRepository;
	private readonly IPlaylistTreeComposer _playlistTreeComposer;

	private List<PlaylistTreeItem> _roots = new();
	public event Action<Playlist>? PlaylistChosen;

	public PlaylistsPresenter(
		IPlaylistsView view,
		ITrackRepository trackRepository,
		IPlaylistTreeComposer playlistTreeComposer)
	{
		_view = view;
		_trackRepository = trackRepository;
		_playlistTreeComposer = playlistTreeComposer;

		_view.PlaylistSelected += OnPlaylistSelected;

		LoadPlaylists();
	}

	private async void LoadPlaylists()
	{
		try
		{
			var playlists = (await _trackRepository.GetPlaylists()).ToList();
			var roots = await _playlistTreeComposer.ComposeAsync(playlists);
			_roots = roots.ToList();
			_view.SetPlaylistTree(_roots);

			var firstPlaylist = FindFirstSelectablePlaylist(_roots);
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

	/// <inheritdoc/>
	public void LoadPlaylistTree() => LoadPlaylists();

	private static Playlist? FindFirstSelectablePlaylist(IEnumerable<PlaylistTreeItem> items)
	{
		foreach (var item in items)
		{
			var nestedPlaylist = FindFirstSelectablePlaylist(item.Children.OfType<PlaylistTreeItem>());
			if (nestedPlaylist is not null)
			{
				return nestedPlaylist;
			}

			if (item.Playlist is not null)
			{
				return item.Playlist;
			}
		}

		return null;
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
