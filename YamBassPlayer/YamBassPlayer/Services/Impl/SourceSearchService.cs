using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Resolves searchable music sources by source identifier and delegates search to the matching source.
/// </summary>
public sealed class SourceSearchService : ISourceSearchService
{
	private readonly IReadOnlyDictionary<string, IMusicSource> _searchSources;

	public SourceSearchService(IMusicSourceRegistry musicSourceRegistry)
	{
		ArgumentNullException.ThrowIfNull(musicSourceRegistry);

		_searchSources = musicSourceRegistry.Sources
			.Where(source => source.SupportsSearch)
			.ToDictionary(source => source.SourceId, StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public bool SupportsSource(string sourceId)
		=> !string.IsNullOrWhiteSpace(sourceId) && _searchSources.ContainsKey(sourceId);

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

	private IMusicSource GetRequiredSource(string sourceId)
	{
		if (!_searchSources.TryGetValue(sourceId, out IMusicSource? source))
			throw new InvalidOperationException($"Search source '{sourceId}' is not registered.");

		return source;
	}
}
