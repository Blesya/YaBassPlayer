using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Optional capability of a music source that can search not only tracks but
/// also artists and albums, and expand an artist/album into its tracks.
/// </summary>
public interface IEntitySearchSource
{
	/// <summary>
	/// Returns whether the source supports searching artists and albums.
	/// </summary>
	bool SupportsEntitySearch { get; }

	/// <summary>
	/// Searches the source for tracks, artists, and albums matching the query.
	/// At most <paramref name="maxResults"/> results are returned per type.
	/// </summary>
	Task<SearchResult> SearchAllAsync(string query, int maxResults, CancellationToken ct = default);

	/// <summary>
	/// Returns the top tracks of the given artist.
	/// </summary>
	Task<IEnumerable<Track>> GetArtistTracksAsync(string artistId, CancellationToken ct = default);

	/// <summary>
	/// Returns all tracks of the given album.
	/// </summary>
	Task<IEnumerable<Track>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default);
}
