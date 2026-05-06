using Terminal.Gui;
using Terminal.Gui.Trees;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class PlaylistsView : View, IPlaylistsView
{
	private readonly FrameView _panel;
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

		_panel = new FrameView("Плейлисты")
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};

		_panel.Add(_tree);
		Add(_panel);
	}

	public void SetPlaylistTree(IEnumerable<PlaylistTreeItem> roots)
	{
		_roots = roots.ToList();
		Application.MainLoop.Invoke(() =>
		{
			_tree.ClearObjects();
			_tree.AddObjects(_roots.Cast<ITreeNode>().ToList());
			ExpandDefaultNodes(_roots);
			ExpandPathToFirstPlaylist(_roots);
			_tree.SetNeedsDisplay();
		});
	}

	public void AddOrUpdateTransientPlaylist(Playlist playlist)
	{
		Application.MainLoop.Invoke(() =>
		{
			var item = PlaylistTreeItem.FromPlaylist(playlist);

			if (playlist.ParentTag is not null)
			{
				var parent = FindNodeByTag(_roots, playlist.ParentTag);
				if (parent is not null)
				{
					var existing = parent.Children
						.OfType<PlaylistTreeItem>()
						.FirstOrDefault(c => c.Playlist?.Type == playlist.Type);
					if (existing is not null)
						parent.Children.Remove(existing);
					parent.Children.Add(item);
					parent.UpdateText();
					_tree.SetNeedsDisplay();
					return;
				}
			}

			var existingRoot = _roots.FirstOrDefault(r => r.Playlist?.Type == playlist.Type);
			if (existingRoot is not null)
			{
				existingRoot.Playlist = playlist;
				existingRoot.UpdateText();
			}
			else
			{
				_roots.Add(item);
				_tree.AddObject(item);
			}

			_tree.SetNeedsDisplay();
		});
	}

	private static PlaylistTreeItem? FindNodeByTag(IEnumerable<PlaylistTreeItem> items, string tag)
	{
		foreach (var item in items)
		{
			if (item.Tag is string s && s == tag)
				return item;
			var found = FindNodeByTag(item.Children.OfType<PlaylistTreeItem>(), tag);
			if (found is not null)
				return found;
		}
		return null;
	}

	public void MarkAsPlaying(Playlist? playlist)
	{
		Application.MainLoop.Invoke(() =>
		{
			foreach (var root in _roots)
			{
				UpdatePlayingMark(root, playlist);
			}

			EnsurePlaylistVisible(playlist);
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

	private void ExpandDefaultNodes(IEnumerable<PlaylistTreeItem> items)
	{
		foreach (var item in items)
		{
			if (item.IsExpandedByDefault)
			{
				_tree.Expand(item);
			}

			ExpandDefaultNodes(item.Children.OfType<PlaylistTreeItem>());
		}
	}

	private void ExpandPathToFirstPlaylist(IEnumerable<PlaylistTreeItem> items)
	{
		if (TryFindPathToFirstPlaylist(items, out var path))
		{
			ExpandPath(path);
		}
	}

	private void EnsurePlaylistVisible(Playlist? playlist)
	{
		if (playlist is null)
		{
			return;
		}

		if (!TryFindPathToPlaylist(_roots, playlist, out var path))
		{
			return;
		}

		ExpandPath(path);
		_tree.EnsureVisible(path[^1]);
	}

	private void ExpandPath(IReadOnlyList<PlaylistTreeItem> path)
	{
		for (var i = 0; i < path.Count - 1; i++)
		{
			_tree.Expand(path[i]);
		}
	}

	private static bool TryFindPathToFirstPlaylist(
		IEnumerable<PlaylistTreeItem> items,
		out List<PlaylistTreeItem> path)
	{
		foreach (var item in items)
		{
			if (TryFindPathToFirstPlaylist(item, out path))
			{
				return true;
			}
		}

		path = [];
		return false;
	}

	private static bool TryFindPathToFirstPlaylist(PlaylistTreeItem item, out List<PlaylistTreeItem> path)
	{
		path = [item];
		if (item.Playlist is not null)
		{
			return true;
		}

		foreach (var child in item.Children.OfType<PlaylistTreeItem>())
		{
			if (!TryFindPathToFirstPlaylist(child, out var childPath))
			{
				continue;
			}

			path.AddRange(childPath);
			return true;
		}

		path.Clear();
		return false;
	}

	private static bool TryFindPathToPlaylist(
		IEnumerable<PlaylistTreeItem> items,
		Playlist playlist,
		out List<PlaylistTreeItem> path)
	{
		foreach (var item in items)
		{
			if (TryFindPathToPlaylist(item, playlist, out path))
			{
				return true;
			}
		}

		path = [];
		return false;
	}

	private static bool TryFindPathToPlaylist(
		PlaylistTreeItem item,
		Playlist playlist,
		out List<PlaylistTreeItem> path)
	{
		path = [item];
		if (item.Playlist is not null && IsSamePlaylist(item.Playlist, playlist))
		{
			return true;
		}

		foreach (var child in item.Children.OfType<PlaylistTreeItem>())
		{
			if (!TryFindPathToPlaylist(child, playlist, out var childPath))
			{
				continue;
			}

			path.AddRange(childPath);
			return true;
		}

		path.Clear();
		return false;
	}
}
