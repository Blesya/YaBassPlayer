using Terminal.Gui.Trees;

namespace YamBassPlayer.Models;

public class PlaylistTreeItem : ITreeNode
{
	public string Text { get; set; } = string.Empty;

	public IList<ITreeNode> Children { get; set; } = new List<ITreeNode>();

	public object Tag { get; set; } = null!;

	public Playlist? Playlist { get; set; }

	public PlaylistGroup? Group { get; init; }

	public bool IsGroup => Group != null;

	public bool IsPlaying { get; set; }

	public void UpdateText()
	{
		if (Playlist is null) return;
		Text = $"{Playlist.PlaylistName} ({Playlist.TrackCount})";
	}

	public static PlaylistTreeItem FromPlaylist(Playlist playlist)
	{
		var item = new PlaylistTreeItem
		{
			Playlist = playlist,
			Tag = playlist
		};
		item.UpdateText();
		return item;
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
