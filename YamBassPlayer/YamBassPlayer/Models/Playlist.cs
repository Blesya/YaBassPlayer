using YamBassPlayer.Enums;

namespace YamBassPlayer.Models;

public class Playlist
{
	public Playlist(string name, PlaylistType type)
	{
		PlaylistName = name;
		Type = type;
	}

	public string PlaylistName { get; }

	public PlaylistType Type { get; }

	public string Description { get; init; }

	public int TrackCount { get; init; }

	public override string ToString()
	{
		return $"{PlaylistName} ({TrackCount})";
	}
}