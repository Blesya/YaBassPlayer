namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Resolves favorite operations by source identifier and delegates to the matching source service.
/// </summary>
public sealed class TrackFavoriteService : ITrackFavoriteService
{
	private readonly IReadOnlyDictionary<string, ITrackFavoriteSourceService> _favoriteServices;

	public TrackFavoriteService(IEnumerable<ITrackFavoriteSourceService> favoriteServices)
	{
		ArgumentNullException.ThrowIfNull(favoriteServices);

		_favoriteServices = favoriteServices.ToDictionary(service => service.SourceId, StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public bool SupportsSource(string sourceId)
		=> !string.IsNullOrWhiteSpace(sourceId) && _favoriteServices.ContainsKey(sourceId);

	/// <inheritdoc />
	public bool IsTrackFavorite(string sourceId, string trackId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
		return GetRequiredService(sourceId).IsTrackFavorite(trackId);
	}

	/// <inheritdoc />
	public Task AddToFavorites(string sourceId, string trackId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
		return GetRequiredService(sourceId).AddToFavorites(trackId);
	}

	/// <inheritdoc />
	public Task RemoveFromFavorites(string sourceId, string trackId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
		return GetRequiredService(sourceId).RemoveFromFavorites(trackId);
	}

	private ITrackFavoriteSourceService GetRequiredService(string sourceId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

		if (!_favoriteServices.TryGetValue(sourceId, out ITrackFavoriteSourceService? favoriteService))
			throw new InvalidOperationException($"Favorite service for source '{sourceId}' is not registered.");

		return favoriteService;
	}
}
