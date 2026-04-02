namespace YamBassPlayer.Services;

/// <summary>
/// Provides source-oriented access to track favorites without exposing source-specific services.
/// </summary>
public interface ITrackFavoriteService
{
	/// <summary>
	/// Returns whether the specified source has a favorite service registered.
	/// </summary>
	bool SupportsSource(string sourceId);

	/// <summary>
	/// Returns whether the specified track is marked as favorite in the given source.
	/// </summary>
	bool IsTrackFavorite(string sourceId, string trackId);

	/// <summary>
	/// Marks the specified track as favorite in the given source.
	/// </summary>
	Task AddToFavorites(string sourceId, string trackId);

	/// <summary>
	/// Removes the specified track from favorites in the given source.
	/// </summary>
	Task RemoveFromFavorites(string sourceId, string trackId);
}
