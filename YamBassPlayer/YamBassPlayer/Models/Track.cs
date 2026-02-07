namespace YamBassPlayer.Models;

public class Track(string title, string artist, string album, string id)
{
	public string Title { get; } = title;
	public string Artist { get; } = artist;
	public string Album { get; } = album;
	public string Id { get; } = id;

	public override string ToString() => $"{Artist} — {Title}";
}