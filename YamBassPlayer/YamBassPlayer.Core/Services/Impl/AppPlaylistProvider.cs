using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class AppPlaylistProvider(string tracksFolder, ILocalFavoriteService favorites) : IAppPlaylistProvider
{
    public async Task<IReadOnlyList<Playlist>> GetAppPlaylistsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<TrackKey> favoriteKeys = await favorites.GetAllFavoriteKeys();

        IReadOnlyList<Playlist> result =
        [
            new Playlist("favorite", "Локальное избранное")
            {
                Category = PlaylistCategory.Data,
                Description = "Избранное хранится локально",
                TrackCount = favoriteKeys.Count
            },
            new Playlist("cached", "Загруженные")
            {
                Category = PlaylistCategory.Computed,
                Description = "Треки из локального кеша",
                TrackCount = GetCachedTracksCount()
            },
            new Playlist("top10", "Топ 10")
            {
                Category = PlaylistCategory.Computed,
                Description = "Топ 10 треков!",
                TrackCount = 10
            },
            new Playlist("top-evenings", "Топ вечеров")
            {
                Category = PlaylistCategory.Computed,
                Description = "Топ треков с 16:00 до 24:00",
                TrackCount = 20
            },
        ];

        return result;
    }

    private int GetCachedTracksCount()
    {
        if (!Directory.Exists(tracksFolder))
            return 0;

        return Directory.GetDirectories(tracksFolder)
            .SelectMany(folder => Directory.GetFiles(folder, "*.mp3"))
            .Concat(Directory.GetFiles(tracksFolder, "*.mp3", SearchOption.TopDirectoryOnly))
            .Count();
    }
}
