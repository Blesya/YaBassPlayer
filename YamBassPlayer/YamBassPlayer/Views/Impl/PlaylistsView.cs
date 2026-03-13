using Terminal.Gui;
using Terminal.Gui.Trees;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class PlaylistsView : View, IPlaylistsView
{
	private readonly TreeView _tree;
	private List<PlaylistTreeItem> _roots = new();

	public event Action<Playlist>? PlaylistSelected;

	public PlaylistsView()
	{
		Width = Dim.Fill();
		Height = Dim.Fill();

		_tree = new TreeView
		{
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			MultiSelect = false
		};

		_tree.SelectionChanged += (_, args) =>
		{
			if (args.NewValue is PlaylistTreeItem { Playlist: not null } item)
			{
				PlaylistSelected?.Invoke(item.Playlist);
			}
		};

		Add(_tree);
	}

	public void SetPlaylistTree(IEnumerable<PlaylistTreeItem> roots)
	{
		_roots = roots.ToList();
		Application.MainLoop.Invoke(() =>
		{
			_tree.ClearObjects();
			_tree.AddObjects(_roots.Cast<ITreeNode>().ToList());
		});
	}

	public void AddOrUpdateTransientPlaylist(Playlist playlist)
	{
		Application.MainLoop.Invoke(() =>
		{
			var existing = _roots.FirstOrDefault(r => r.Playlist?.Type == playlist.Type);
			if (existing is not null)
			{
				existing.Playlist = playlist;
				existing.UpdateText();
			}
			else
			{
				var item = PlaylistTreeItem.FromPlaylist(playlist);
				_roots.Add(item);
				_tree.AddObject(item);
			}

			_tree.SetNeedsDisplay();
		});
	}

	public void MarkAsPlaying(Playlist? playlist)
	{
		Application.MainLoop.Invoke(() =>
		{
			foreach (var root in _roots)
			{
				UpdatePlayingMark(root, playlist);
			}

			_tree.SetNeedsDisplay();
		});
	}

	private static bool IsSamePlaylist(Playlist a, Playlist b)
		=> a.PlaylistName == b.PlaylistName && a.Type == b.Type;

	private static void UpdatePlayingMark(PlaylistTreeItem item, Playlist? playing)
	{
		if (item.Playlist is not null)
		{
			item.IsPlaying = playing is not null && IsSamePlaylist(item.Playlist, playing);
			item.UpdateText();
		}

		foreach (var child in item.Children.OfType<PlaylistTreeItem>())
		{
			UpdatePlayingMark(child, playing);
		}
	}
}