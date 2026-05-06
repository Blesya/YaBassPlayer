using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class FavoritesLoadStrategy(
    ITrackRepositoryCache cache,
    ILocalFavoriteService localFavoriteService)
    : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type)
        => type is PlaylistType.Favorite or PlaylistType.LocalFavorite;

    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.Run(async () =>
        {
            return playlist.Type == PlaylistType.Favorite
                ? cache.FavoriteTrackIds.ToList()
                : await localFavoriteService.GetAllFavoriteTrackIds();
        });
}

public sealed class Top10LoadStrategy(IHistoryService historyService) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.Top10;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(historyService.GetTopTracks(10).Select(x => x.trackId).ToList());
}

public sealed class TopEveningsLoadStrategy(IHistoryService historyService) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.TopEvenings;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(historyService.GetTopEveningTracks(20).Select(x => x.trackId).ToList());
}

public sealed class TopByDayLoadStrategy(IHistoryService historyService) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.TopByDay;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(
            historyService.GetTopTracksByDayOfWeek(playlist.DayOfWeek!.Value, 50)
                .Select(x => x.trackId).ToList());
}

public sealed class CachedLoadStrategy(string tracksFolder) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.Cached;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.Run(() =>
        {
            if (!Directory.Exists(tracksFolder))
                return new List<string>();

            return Directory.GetFiles(tracksFolder, "*.mp3")
                .Select(filePath => new FileInfo(filePath))
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        });
}

public sealed class PlaylistOfTheDayLoadStrategy(IMusicSourceRegistry musicSourceRegistry) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.PlaylistOfTheDaily;
    public async Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
    {
        var yandexSource = musicSourceRegistry.GetRequired(SourceIds.Yandex);
        var p = new Playlist(string.Empty, PlaylistType.PlaylistOfTheDaily);
        var tracks = await yandexSource.GetPlaylistTracksAsync(p, 0, int.MaxValue);
        return tracks.Select(t => t.Id).ToList();
    }
}

public sealed class CustomLoadStrategy(
    IMusicSourceRegistry musicSourceRegistry,
    ITrackRepositoryCache cache)
    : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.Custom;
    public async Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
    {
        if (cache.TryGetCustomPlaylistIds(playlist.PlaylistName, out var cachedIds))
            return cachedIds;

        var yandexSource = musicSourceRegistry.GetRequired(SourceIds.Yandex);
        var playlists = await yandexSource.GetPlaylistsAsync();
        var found = playlists.FirstOrDefault(
            p => p.PlaylistName == playlist.PlaylistName && p.Type == PlaylistType.Custom);

        if (found is null)
            throw new InvalidOperationException($"Playlist '{playlist.PlaylistName}' not found");

        var tracks = (await yandexSource.GetPlaylistTracksAsync(found, 0, int.MaxValue)).ToList();
        var trackIds = tracks.Select(t => t.Id).ToList();
        cache.SetCustomPlaylistIds(playlist.PlaylistName, trackIds);

        return trackIds;
    }
}

public sealed class ArtistLoadStrategy(ITrackInfoProvider trackInfoProvider) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.Artist;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => trackInfoProvider.GetTrackIdsByArtistAsync(playlist.PlaylistName);
}

public sealed class QueueLoadStrategy(ITrackRepositoryCache cache) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.Queue;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(cache.QueueTrackIds.ToList());
}

public sealed class MyWaveLoadStrategy(ITrackRepositoryCache cache) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.MyWave;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(cache.MyWaveTracks.Select(t => t.Id).ToList());
}

public sealed class LocalSearchLoadStrategy(ITrackRepositoryCache cache) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.LocalSearch;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(cache.LocalSearchTrackIds.ToList());
}

public sealed class YandexSearchLoadStrategy(ITrackRepositoryCache cache) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.YandexSearch;
    public Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(cache.YandexSearchTrackIds.ToList());
}

public sealed class LocalFolderLoadStrategy(IMusicSourceRegistry musicSourceRegistry) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.LocalFolder;
    public async Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
    {
        var localSource = musicSourceRegistry.Get(SourceIds.Local);
        if (localSource is null)
            return [];

        var tracks = await localSource.GetPlaylistTracksAsync(playlist, 0, int.MaxValue);
        return tracks.Select(t => t.Id).ToList();
    }
}

public sealed class LocalArtistLoadStrategy(ILocalLibraryService localLibraryService) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.LocalArtist;
    public async Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
    {
        var tracks = await localLibraryService.GetTracksByArtistAsync(playlist.PlaylistName);
        return tracks.Select(t => t.Id).ToList();
    }
}

public sealed class LocalAlbumLoadStrategy(ILocalLibraryService localLibraryService) : IPlaylistLoadStrategy
{
    public bool CanHandle(PlaylistType type) => type == PlaylistType.LocalAlbum;
    public async Task<List<string>> LoadTrackIdsAsync(Playlist playlist)
    {
        // Description encodes "artistName\nalbumName" — empty artist means all artists.
        var parts = playlist.Description?.Split('\n', 2);
        if (parts?.Length != 2)
            return [];

        string artistName = parts[0];
        string albumName = parts[1];

        IReadOnlyList<Track> tracks = string.IsNullOrEmpty(artistName)
            ? await localLibraryService.GetTracksByAlbumTitleAsync(albumName)
            : await localLibraryService.GetTracksByAlbumAsync(artistName, albumName);

        return tracks.Select(t => t.Id).ToList();
    }
}
