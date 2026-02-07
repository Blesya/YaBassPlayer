using YamBassPlayer.Enums;

namespace YamBassPlayer.Models;

public class Playlist(string name, PlaylistType type)
{
	public string PlaylistName { get; } = name;

	public PlaylistType Type { get; } = type;

	public string Description { get; init; }

	public int TrackCount { get; init; }

	public DayOfWeek? DayOfWeek { get; init; }

	public override string ToString()
	{
		return $"{PlaylistName} ({TrackCount})";
	}
}