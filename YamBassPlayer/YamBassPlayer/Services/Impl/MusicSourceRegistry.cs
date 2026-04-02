namespace YamBassPlayer.Services.Impl;

public sealed class MusicSourceRegistry : IMusicSourceRegistry
{
    private readonly IReadOnlyList<IMusicSource> _sources;
    private readonly Dictionary<string, IMusicSource> _sourcesById;

    public MusicSourceRegistry(IEnumerable<IMusicSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var sourceList = sources.ToList();
        _sources = sourceList;
        _sourcesById = new Dictionary<string, IMusicSource>(sourceList.Count, StringComparer.Ordinal);

        foreach (var source in sourceList)
        {
            if (!_sourcesById.TryAdd(source.SourceId, source))
            {
                throw new InvalidOperationException($"Music source '{source.SourceId}' is registered more than once.");
            }
        }
    }

    public IReadOnlyList<IMusicSource> Sources => _sources;

    public IMusicSource? Get(string sourceId)
    {
        _sourcesById.TryGetValue(sourceId, out var source);
        return source;
    }

    public IMusicSource GetRequired(string sourceId)
    {
        if (!_sourcesById.TryGetValue(sourceId, out var source))
            throw new InvalidOperationException($"Music source '{sourceId}' is not registered.");
        return source;
    }
}
