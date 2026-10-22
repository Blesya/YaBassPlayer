namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Resolves the <see cref="IPlaylistLoadStrategy"/> for a core-owned playlist kind.
/// Strategies are auto-discovered via DI (IEnumerable&lt;IPlaylistLoadStrategy&gt;).
/// </summary>
public sealed class PlaylistLoadStrategyResolver
{
    private readonly Dictionary<string, IPlaylistLoadStrategy> _strategyMap;

    public PlaylistLoadStrategyResolver(IEnumerable<IPlaylistLoadStrategy> strategies)
    {
        _strategyMap = new Dictionary<string, IPlaylistLoadStrategy>(StringComparer.Ordinal);
        foreach (var strategy in strategies)
            _strategyMap.TryAdd(strategy.Kind, strategy);
    }

    /// <summary>
    /// Returns the strategy for the given playlist kind.
    /// Throws <see cref="InvalidOperationException"/> if no strategy is registered.
    /// </summary>
    public IPlaylistLoadStrategy Resolve(string kind)
    {
        if (_strategyMap.TryGetValue(kind, out var strategy))
            return strategy;

        throw new InvalidOperationException(
            $"No IPlaylistLoadStrategy registered for playlist kind '{kind}'");
    }
}
