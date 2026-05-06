using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Resolves searchable music sources by source identifier and delegates search to the matching source.
/// </summary>
public sealed class SourceSearchService : ISourceSearchService
{
	private readonly IReadOnlyDictionary<string, IMusicSource> _searchSources;
	private readonly IReadOnlyDictionary<string, IEntitySearchSource> _entitySearchSources;

	public SourceSearchService(IMusicSourceRegistry musicSourceRegistry)
	{
		ArgumentNullException.ThrowIfNull(musicSourceRegistry);

		_searchSources = musicSourceRegistry.Sources
			.Where(source => source.SupportsSearch)
			.ToDictionary(source => source.SourceId, StringComparer.Ordinal);

		_entitySearchSources = musicSourceRegistry.Sources
			.Where(source => source.SupportsSearch && source is IEntitySearchSource entitySource && entitySource.SupportsEntitySearch)
			.ToDictionary(source => source.SourceId, source => (IEntitySearchSource)source, StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public bool SupportsSource(string sourceId)
		=> !string.IsNullOrWhiteSpace(sourceId) && _searchSources.ContainsKey(sourceId);

	/// <inheritdoc />
	public bool SupportsEntitySearch(string sourceId)
		=> !string.IsNullOrWhiteSpace(sourceId) && _entitySearchSources.ContainsKey(sourceId);

	/// <inheritdoc />
	public async Task<IEnumerable<Track>> SearchAsync(string sourceId, string query, int maxResults = int.MaxValue)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

		if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
			return [];

		var source = GetRequiredSource(sourceId);
		var tracks = await source.SearchAsync(query);
		return tracks.Take(maxResults).ToList();
	}

	/// <inheritdoc />
	public async Task<SearchResult> SearchAllAsync(string sourceId, string query, int maxResults = 20)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

		if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
			return new SearchResult();

		var source = GetRequiredEntitySource(sourceId);
		return await source.SearchAllAsync(query, maxResults);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Track>> GetArtistTracksAsync(string sourceId, string artistId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
		ArgumentException.ThrowIfNullOrWhiteSpace(artistId);

		var source = GetRequiredEntitySource(sourceId);
		return await source.GetArtistTracksAsync(artistId);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Track>> GetAlbumTracksAsync(string sourceId, string albumId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
		ArgumentException.ThrowIfNullOrWhiteSpace(albumId);

		var source = GetRequiredEntitySource(sourceId);
		return await source.GetAlbumTracksAsync(albumId);
	}

	private IMusicSource GetRequiredSource(string sourceId)
	{
		if (!_searchSources.TryGetValue(sourceId, out IMusicSource? source))
			throw new InvalidOperationException($"Search source '{sourceId}' is not registered.");

		return source;
	}

	private IEntitySearchSource GetRequiredEntitySource(string sourceId)
	{
		if (!_entitySearchSources.TryGetValue(sourceId, out IEntitySearchSource? source))
			throw new InvalidOperationException($"Entity search is not supported for source '{sourceId}'.");

		return source;
	}
}
