namespace YamBassPlayer.Models;

/// <summary>
/// A unified result of an entity search, encapsulating the tracks, artists,
/// and albums returned by a single query.
/// </summary>
public sealed record SearchResult
{
	public IReadOnlyList<Track> Tracks { get; init; } = [];
	public IReadOnlyList<Artist> Artists { get; init; } = [];
	public IReadOnlyList<Album> Albums { get; init; } = [];

	public bool IsEmpty => Tracks.Count == 0 && Artists.Count == 0 && Albums.Count == 0;
}

/// <summary>
/// A single selectable entity produced by a search, used by views to present
/// and mark heterogeneous results (tracks, artists, albums) in one list.
/// </summary>
public abstract record SearchResultItem;

public sealed record TrackItem(Track Track) : SearchResultItem;
public sealed record ArtistItem(Artist Artist) : SearchResultItem;
public sealed record AlbumItem(Album Album) : SearchResultItem;
