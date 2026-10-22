namespace YamBassPlayer.Models;

public class LocalFolder(int id, string path, string name)
{
	public int Id { get; } = id;
	public string Path { get; } = path;
	public string Name { get; } = name;
	public DateTimeOffset AddedAt { get; init; }
	public DateTimeOffset? LastScannedAt { get; init; }

	public override string ToString() => $"{Name} ({Path})";
}
