using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class AppPlaylistProvider(
    ILocalFavoriteService localFavoriteService,
    string tracksFolder) : IAppPlaylistProvider
{
    public async Task<IReadOnlyList<Playlist>> GetAppPlaylistsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var localFavoriteIds = await localFavoriteService.GetAllFavoriteTrackIds();

        return
        [
            new Playlist("Локальное Избранное", PlaylistType.LocalFavorite)
            {
                Description = "Избранные треки (локально)",
                TrackCount = localFavoriteIds.Count
            },
            new Playlist("Загруженные", PlaylistType.Cached)
            {
                Description = "Треки из локального кеша",
                TrackCount = GetCachedTracksCount()
            },
            new Playlist("Топ 10", PlaylistType.Top10)
            {
                Description = "Топ 10 треков!",
                TrackCount = 10
            },
            new Playlist("Топ вечеров", PlaylistType.TopEvenings)
            {
                Description = "Топ треков с 16:00 до 24:00",
                TrackCount = 20
            },
        ];
    }

    private int GetCachedTracksCount()
    {
        if (!Directory.Exists(tracksFolder))
            return 0;

        return Directory.GetFiles(tracksFolder, "*.mp3").Length;
    }
}
