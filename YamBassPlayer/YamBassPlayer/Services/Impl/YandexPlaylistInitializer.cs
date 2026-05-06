using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class YandexPlaylistInitializer(
    IMusicSourceRegistry musicSourceRegistry,
    IYandexFavoriteService yandexFavoriteService,
    ITrackRepositoryCache cache,
    ITrackInfoProvider trackInfoProvider) : IYandexPlaylistInitializer
{
    private IMusicSource YandexSource => musicSourceRegistry.GetRequired(SourceIds.Yandex);

    public async Task<int> InitializeAsync(IReadOnlyList<Playlist> yandexPlaylists, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var favPlaylist = yandexPlaylists.FirstOrDefault(p => p.Type == PlaylistType.Favorite);

        // Load only track IDs, not full metadata — avoids expensive Track.GetAsync API call
        string[] favoriteTrackIds = favPlaylist != null
            ? (await YandexSource.GetPlaylistTrackIdsAsync(favPlaylist, ct)).ToArray()
            : [];
        cache.ReplaceFavoriteTrackIds(favoriteTrackIds);
        yandexFavoriteService.Initialize(favoriteTrackIds);
        yandexFavoriteService.OnFavoriteAdded += cache.InsertFavoriteTrackId;
        yandexFavoriteService.OnFavoriteRemoved += cache.RemoveFavoriteTrackId;

        // For custom playlists: load only track IDs from cached personal playlists data
        foreach (var customPlaylist in yandexPlaylists.Where(p => p.Type == PlaylistType.Custom))
        {
            var trackIds = (await YandexSource.GetPlaylistTrackIdsAsync(customPlaylist, ct)).ToList();
            cache.SetCustomPlaylistIds(customPlaylist.PlaylistName, trackIds);
        }

        return favoriteTrackIds.Length;
    }
}
