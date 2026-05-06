using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Provides source-oriented search without exposing source-specific APIs.
/// </summary>
public interface ISourceSearchService
{
	/// <summary>
	/// Returns whether the specified source has search support registered.
	/// </summary>
	bool SupportsSource(string sourceId);

	/// <summary>
	/// Returns whether the specified source can search artists and albums.
	/// </summary>
	bool SupportsEntitySearch(string sourceId);

	/// <summary>
	/// Searches the specified source and returns matching tracks.
	/// </summary>
	Task<IEnumerable<Track>> SearchAsync(string sourceId, string query, int maxResults = int.MaxValue);

	/// <summary>
	/// Searches the specified source for tracks, artists, and albums.
	/// At most <paramref name="maxResults"/> results are returned per type.
	/// </summary>
	Task<SearchResult> SearchAllAsync(string sourceId, string query, int maxResults = 20);

	/// <summary>
	/// Returns the top tracks of the given artist.
	/// </summary>
	Task<IEnumerable<Track>> GetArtistTracksAsync(string sourceId, string artistId);

	/// <summary>
	/// Returns all tracks of the given album.
	/// </summary>
	Task<IEnumerable<Track>> GetAlbumTracksAsync(string sourceId, string albumId);
}
