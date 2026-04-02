using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Provides source-oriented track search without exposing source-specific APIs.
/// </summary>
public interface ISourceSearchService
{
	/// <summary>
	/// Returns whether the specified source has search support registered.
	/// </summary>
	bool SupportsSource(string sourceId);

	/// <summary>
	/// Searches the specified source and returns matching tracks.
	/// </summary>
	Task<IEnumerable<Track>> SearchAsync(string sourceId, string query, int maxResults = int.MaxValue);
}
