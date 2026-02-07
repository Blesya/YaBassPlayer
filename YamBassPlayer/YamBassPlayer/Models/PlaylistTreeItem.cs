using Terminal.Gui.Trees;

namespace YamBassPlayer.Models;

public class PlaylistTreeItem : ITreeNode
{
	public string Text { get; set; } = string.Empty;

	public IList<ITreeNode> Children { get; set; } = new List<ITreeNode>();

	public object Tag { get; set; } = null!;

	public Playlist? Playlist { get; init; }

	public PlaylistGroup? Group { get; init; }

	public bool IsGroup => Group != null;

	public static PlaylistTreeItem FromPlaylist(Playlist playlist)
	{
		return new PlaylistTreeItem
		{
			Text = playlist.ToString(),
			Playlist = playlist,
			Tag = playlist
		};
	}

	public static PlaylistTreeItem FromGroup(PlaylistGroup group)
	{
		var children = group.Children
			.Select(FromPlaylist)
			.Cast<ITreeNode>()
			.ToList();

		return new PlaylistTreeItem
		{
			Text = group.Name,
			Group = group,
			Children = children,
			Tag = group
		};
	}
}
