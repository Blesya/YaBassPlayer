namespace YamBassPlayer.Services;

/// <summary>
/// Exposes favorite operations for a single music source.
/// </summary>
public interface ITrackFavoriteSourceService
{
	/// <summary>
	/// Gets the source identifier handled by this service.
	/// </summary>
	string SourceId { get; }

	/// <summary>
	/// Returns whether the specified track is marked as favorite in this source.
	/// </summary>
	bool IsTrackFavorite(string trackId);

	/// <summary>
	/// Marks the specified track as favorite in this source.
	/// </summary>
	Task AddToFavorites(string trackId);

	/// <summary>
	/// Removes the specified track from favorites in this source.
	/// </summary>
	Task RemoveFromFavorites(string trackId);

	event Action<string>? OnFavoriteAdded;
	event Action<string>? OnFavoriteRemoved;
}
