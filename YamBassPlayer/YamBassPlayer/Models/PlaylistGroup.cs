namespace YamBassPlayer.Models;

public class PlaylistGroup(string name, IReadOnlyList<Playlist> children, bool isExpanded = false)
{
	public string Name { get; } = name;

	public IReadOnlyList<Playlist> Children { get; } = children;

	public bool IsExpanded { get; set; } = isExpanded;
}
