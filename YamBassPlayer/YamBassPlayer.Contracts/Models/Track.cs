namespace YamBassPlayer.Models;

public class Track(string title, string artist, string album, string id)
{
	public string Title { get; } = title;
	public string Artist { get; } = artist;
	public string Album { get; } = album;
	public string Id { get; } = id;
	public string? LocalFilePath { get; init; }
	public string? Subtitle { get; init; }
	public long? DurationMs { get; init; }
	public int? Year { get; init; }
	public int? TrackNumber { get; init; }
	public string? CoverUrl { get; init; }
	public string? RemoteCoverUrl { get; init; }
	public string? LocalCoverPath { get; init; }
	public IReadOnlyList<string>? Genres { get; init; }
	public required SourceId SourceId { get; init; }
	public IReadOnlyList<Artist>? Artists { get; init; }
	public Album? AlbumInfo { get; init; }

	public TrackKey Key => new(SourceId, Id);

	public override string ToString() => $"{Artist} — {Title}";
}
