using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class MusicSourceRegistry : IMusicSourceRegistry
{
    private readonly List<IMusicSource> _sources;
    private readonly Dictionary<string, IMusicSource> _sourcesById;

    public MusicSourceRegistry(IEnumerable<IMusicSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = [];
        _sourcesById = new Dictionary<string, IMusicSource>(StringComparer.Ordinal);

        foreach (IMusicSource source in sources)
            Add(source);
    }

    public IReadOnlyList<IMusicSource> Sources => _sources;

    public IMusicSource? Get(SourceId id)
    {
        _sourcesById.TryGetValue(id.Value, out IMusicSource? source);
        return source;
    }

    public IMusicSource GetRequired(SourceId id)
    {
        if (!_sourcesById.TryGetValue(id.Value, out IMusicSource? source))
            throw new InvalidOperationException($"Music source '{id}' is not registered.");
        return source;
    }

    public void Add(IMusicSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!_sourcesById.TryAdd(source.Id.Value, source))
            throw new InvalidOperationException($"Music source '{source.Id}' is registered more than once.");

        _sources.Add(source);
    }
}
