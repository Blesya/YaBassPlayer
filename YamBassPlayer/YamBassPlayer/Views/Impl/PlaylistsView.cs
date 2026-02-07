using Terminal.Gui;
using Terminal.Gui.Trees;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class PlaylistsView : View, IPlaylistsView
{
	private readonly TreeView _tree;

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
		Application.MainLoop.Invoke(() =>
		{
			_tree.ClearObjects();
			_tree.AddObjects(roots.Cast<ITreeNode>().ToList());
		});
	}
}