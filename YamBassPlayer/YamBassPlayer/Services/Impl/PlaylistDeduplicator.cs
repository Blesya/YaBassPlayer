using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Убирает повторы одного и того же плейлиста Яндекс.Музыки: лендинг с миксами,
/// список собственных плейлистов и сохранённые чужие приходят из разных ручек
/// и могут пересекаться (например, «Плейлист дня» есть и там, и там).
/// </summary>
public static class PlaylistDeduplicator
{
    /// <summary>
    /// Оставляет первое вхождение каждого плейлиста, опознанного по паре «владелец:kind».
    /// Плейлисты без такой пары (избранное, локальные, поисковые) не трогаем.
    /// </summary>
    public static List<Playlist> ByYandexIdentity(IEnumerable<Playlist> playlists)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Playlist>();

        foreach (var playlist in playlists)
        {
            if (playlist.Payload is not { YandexOwnerUid: not null, YandexKind: not null })
            {
                result.Add(playlist);
                continue;
            }

            if (seen.Add(playlist.TrackCacheKey))
                result.Add(playlist);
        }

        return result;
    }
}
