using Terminal.Gui.Trees;

namespace YamBassPlayer.Models;

public class PlaylistTreeItem : ITreeNode
{
	private const string PlayingPrefix = "▶ ";

	public string Label { get; set; } = string.Empty;

	public string Text { get; set; } = string.Empty;

	public IList<ITreeNode> Children { get; set; } = new List<ITreeNode>();

	public object Tag { get; set; } = null!;

	public Playlist? Playlist { get; set; }

	public PlaylistGroup? Group { get; init; }

	public bool IsGroup => Group != null;

	public bool IsPlaying { get; set; }

	public bool IsExpandedByDefault { get; init; }

	public void UpdateText()
	{
		if (Playlist is not null)
		{
			var prefix = IsPlaying ? PlayingPrefix : string.Empty;
			Text = $"{prefix}{Playlist.PlaylistName} ({Playlist.TrackCount})";
			return;
		}

		var childCount = Children.OfType<PlaylistTreeItem>().Count();
		Text = childCount > 0
			? $"{Label} [{childCount}]"
			: Label;
	}

	public static PlaylistTreeItem FromPlaylist(Playlist playlist)
	{
		var item = new PlaylistTreeItem
		{
			Label = playlist.PlaylistName,
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

		var item = new PlaylistTreeItem
		{
			Label = group.Name,
			Group = group,
			Children = children,
			Tag = group,
			IsExpandedByDefault = group.IsExpanded
		};

		item.UpdateText();
		return item;
	}
}
