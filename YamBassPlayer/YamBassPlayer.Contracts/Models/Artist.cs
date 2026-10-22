namespace YamBassPlayer.Models;

public class Artist(string id, string name)
{
	public string Id { get; } = id;
	public string Name { get; } = name;
	public string? CoverUrl { get; init; }
	public string? Description { get; init; }

	public override string ToString() => Name;
}
