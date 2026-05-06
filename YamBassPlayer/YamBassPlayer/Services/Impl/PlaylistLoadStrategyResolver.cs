using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Resolves the appropriate <see cref="IPlaylistLoadStrategy"/> for a given <see cref="PlaylistType"/>.
/// Strategies are auto-discovered via DI (IEnumerable&lt;IPlaylistLoadStrategy&gt;).
/// </summary>
public sealed class PlaylistLoadStrategyResolver
{
    private readonly Dictionary<PlaylistType, IPlaylistLoadStrategy> _strategyMap;

    public PlaylistLoadStrategyResolver(IEnumerable<IPlaylistLoadStrategy> strategies)
    {
        _strategyMap = new Dictionary<PlaylistType, IPlaylistLoadStrategy>();
        foreach (var strategy in strategies)
        {
            foreach (PlaylistType type in Enum.GetValues<PlaylistType>())
            {
                if (strategy.CanHandle(type) && !_strategyMap.ContainsKey(type))
                    _strategyMap[type] = strategy;
            }
        }
    }

    /// <summary>
    /// Returns the strategy for the given playlist type.
    /// Throws <see cref="InvalidOperationException"/> if no strategy is registered.
    /// </summary>
    public IPlaylistLoadStrategy Resolve(PlaylistType type)
    {
        if (_strategyMap.TryGetValue(type, out var strategy))
            return strategy;

        throw new InvalidOperationException(
            $"No IPlaylistLoadStrategy registered for PlaylistType.{type}");
    }
}
