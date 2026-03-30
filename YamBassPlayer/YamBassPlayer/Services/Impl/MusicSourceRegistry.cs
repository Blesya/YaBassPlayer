namespace YamBassPlayer.Services.Impl;

public sealed class MusicSourceRegistry : IMusicSourceRegistry
{
    private readonly Dictionary<string, IMusicSource> _sources = new();

    public IReadOnlyList<IMusicSource> Sources => _sources.Values.ToList();

    public void Register(IMusicSource source)
    {
        _sources[source.SourceId] = source;
    }

    public IMusicSource? Get(string sourceId)
    {
        _sources.TryGetValue(sourceId, out var source);
        return source;
    }

    public IMusicSource GetRequired(string sourceId)
    {
        if (!_sources.TryGetValue(sourceId, out var source))
            throw new InvalidOperationException($"Music source '{sourceId}' is not registered.");
        return source;
    }
}
