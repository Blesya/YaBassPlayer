namespace YamBassPlayer.Models;

public class Album(string id, string title)
{
	public string Id { get; } = id;
	public string Title { get; } = title;
	public int? Year { get; init; }
	public string? CoverUrl { get; init; }
	public string? Genre { get; init; }
	public int? TrackCount { get; init; }
	public IReadOnlyList<string>? ArtistIds { get; init; }

	public override string ToString() => Title;
}
