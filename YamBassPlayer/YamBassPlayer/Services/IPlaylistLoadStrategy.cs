using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Strategy pattern interface for loading playlist track IDs.
/// Each playlist type has its own strategy implementation.
/// </summary>
public interface IPlaylistLoadStrategy
{
    /// <summary>Returns true if this strategy can handle the given playlist type.</summary>
    bool CanHandle(PlaylistType type);

    /// <summary>Loads track IDs for the given playlist.</summary>
    Task<List<string>> LoadTrackIdsAsync(Playlist playlist);
}
