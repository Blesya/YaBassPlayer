using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Убирает повторы одного и того же плейлиста, пришедшего из разных ручек источника.
/// </summary>
public static class PlaylistDeduplicator
{
    /// <summary>
    /// Оставляет первое вхождение каждого плейлиста по его <see cref="Playlist.CacheKey"/>.
    /// </summary>
    public static List<Playlist> ByCacheKey(IEnumerable<Playlist> playlists)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Playlist>();

        foreach (var playlist in playlists)
        {
            if (seen.Add(playlist.CacheKey))
                result.Add(playlist);
        }

        return result;
    }
}
