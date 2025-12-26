using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Library;
using Yandex.Music.Api.Models.Playlist;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public class TrackRepository : ITrackRepository
{
    private readonly YandexMusicApi _api;
    private readonly AuthStorage _storage;
    private readonly ITrackInfoProvider _trackInfoProvider;
    private readonly string _tracksFolder;
    private readonly IHistoryService _historyService;
    private readonly ILocalFavoriteService _localFavoriteService;

    private List<string> _tracksIds = new();
    private Playlist _currentPlaylist;
    private int _currentOffset = 0;
    private readonly Dictionary<string, List<string>> _customPlaylistCache = new();
    private readonly List<string> _favoritePlaylistCache = new();

    public TrackRepository(
        YandexMusicApi api,
        AuthStorage storage,
        ITrackInfoProvider trackInfoProvider,
        string tracksFolder,
        IHistoryService historyService,
        ILocalFavoriteService localFavoriteService)
    {
        _api = api;
        _storage = storage;
        _tracksFolder = tracksFolder;
        _historyService = historyService;
        _trackInfoProvider = trackInfoProvider;
        _localFavoriteService = localFavoriteService;
    }

    public async Task<IEnumerable<Playlist>> GetPlaylists()
    {
        try
        {
            IEnumerable<YResponse<YPlaylist>>? yResponses = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);

            YResponse<YLibraryTracks>? liked = await _api.Library.GetLikedTracksAsync(_storage);
            string[] favoriteTrackIds = liked.Result.Library.Tracks.Select(x => x.Id).ToArray();
            _favoritePlaylistCache.AddRange(favoriteTrackIds);
            int likedTracksCount = favoriteTrackIds.Length;

            var localFavoriteIds = await _localFavoriteService.GetAllFavoriteTrackIds();

            List<Playlist> playlists =
            [
                new Playlist("Мои треки", PlaylistType.Favorite)
                {
                    Description = "Треки, которые вам понравились",
                    TrackCount = likedTracksCount
                },
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
                }
            ];

            foreach (YResponse<YPlaylist> yResponse in yResponses)
            {
                List<YTrackContainer>? yTrackContainers = yResponse.Result.Tracks;
                IEnumerable<Track> tracks = yTrackContainers.Select(yTrackContainer => yTrackContainer.Track.ToTrack());
                foreach (Track track in tracks)
                {
                    await _trackInfoProvider.SaveAsync(track);
                }

                List<string> trackIds = yTrackContainers.Select(t => t.Id).ToList();
                string? playlistTitle = yResponse.Result.Title;
                _customPlaylistCache[playlistTitle] = trackIds;

                Playlist playlist = new Playlist(playlistTitle, PlaylistType.Custom)
                {
                    Description = yResponse.Result.Description,
                    TrackCount = yResponse.Result.TrackCount
                };

                playlists.Add(playlist);
            }

            return playlists;
        }
        catch (Exception exception)
        {
            exception.Handle();
            return [];
        }
    }

    public async Task SetPlaylist(Playlist playlist)
    {
        try
        {
            switch (playlist.Type)
            {
                case PlaylistType.Favorite:
                    await SetFavorite(playlist);
                    break;
                case PlaylistType.PlaylistOfTheDaily:
                    await SetPlaylistOfTheDay(playlist);
                    break;
                case PlaylistType.Custom:
                    await SetCustomPlaylist(playlist);
                    break;
                case PlaylistType.Cached:
                    await SetCachedPlaylist(playlist);
                    break;
                case PlaylistType.Top10:
                    await SetTop10Playlist(playlist);
                    break;
                case PlaylistType.LocalFavorite:
                    await SetLocalFavoritePlaylist(playlist);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _currentPlaylist = playlist;
        }
        catch (Exception exception)
        {
            exception.Handle();
        }
    }

    private Task SetTop10Playlist(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadTop10Playlist);

    private Task<List<string>> LoadTop10Playlist()
        => Task.Run(() =>
        {
            List<string> day = _historyService.GetTopTracks(10)
                .Select(x => x.trackId)
                .ToList();
            return Task.FromResult(day);
        });

    private Task SetCustomPlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, () => LoadCustomPlaylistAsync(playlist.PlaylistName));

    private Task SetCachedPlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadCachedTracksAsync);

    private Task SetFavorite(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadFavoritesAsync);

    private Task SetLocalFavoritePlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadLocalFavoritesAsync);

    private Task SetPlaylistOfTheDay(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadPlaylistOfTheDayAsync);

    private async Task SetPlaylistAsync(Playlist playlist, Func<Task<List<string>>> loadTracks)
    {
        var trackIds = await loadTracks();

        _tracksIds = trackIds;
        _currentOffset = 0;
        _currentPlaylist = playlist;
    }

    private Task<List<string>> LoadCustomPlaylistAsync(string playlistName)
    {
        try
        {
            return Task.Run(async () =>
            {
                if (_customPlaylistCache.TryGetValue(playlistName, out var cachedIds))
                {
                    return cachedIds;
                }

                var playlists = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);
                var found = playlists.FirstOrDefault(x => x.Result.Title == playlistName)
                            ?? throw new InvalidOperationException($"Playlist '{playlistName}' not found");

                var trackIds = found.Result.Tracks.Select(t => t.Id).ToList();
                _customPlaylistCache[playlistName] = trackIds;

                return trackIds;
            });
        }
        catch (Exception exception)
        {
            exception.Handle();
            return new Task<List<string>>(() => new List<string>());
        }
    }

    private Task<List<string>> LoadFavoritesAsync()
        => Task.Run(async () =>
        {
            //var liked = await _api.Library.GetLikedTracksAsync(_storage);
            //return liked.Result.Library.Tracks.Select(t => t.Id).ToList();
            return _favoritePlaylistCache;
        });

    private async Task<List<string>> LoadLocalFavoritesAsync()
    {
        return await _localFavoriteService.GetAllFavoriteTrackIds();
    }

    private Task<List<string>> LoadPlaylistOfTheDayAsync()
        => Task.Run(async () =>
        {
            var day = await _api.Playlist.OfTheDayAsync(_storage);
            return day.Result.Tracks.Select(t => t.Id).ToList();
        });

    private Task<List<string>> LoadCachedTracksAsync()
        => Task.Run(() =>
        {
            if (!Directory.Exists(_tracksFolder))
            {
                return new List<string>();
            }

            return Directory.GetFiles(_tracksFolder, "*.mp3")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList()!;
        });

    public int GetCachedTracksCount()
    {
        if (!Directory.Exists(_tracksFolder))
        {
            return 0;
        }

        return Directory.GetFiles(_tracksFolder, "*.mp3").Length;
    }

    public async Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch)
    {
        try
        {
            var slice = _tracksIds
                .Skip(_currentOffset)
                .Take(tracksPerBatch)
                .ToList();

            _currentOffset += tracksPerBatch;

            List<Track> tracksResult = new List<Track>();

            IEnumerable<Track> tracks = await _trackInfoProvider.GetTracksInfoByIds(slice);

            tracksResult.AddRange(tracks);

            return tracksResult;
        }
        catch (Exception exception)
        {
            exception.Handle();
            return [];
        }
    }

    public IReadOnlyList<string> GetAllTrackIds() => _tracksIds.AsReadOnly();

    public async Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount)
    {
        try
        {
            int cachedCount = await _trackInfoProvider.CountCachedTracks(_tracksIds);

            int countToLoad = Math.Max(cachedCount, minCount);
            countToLoad = Math.Min(countToLoad, _tracksIds.Count);

            var idsToLoad = _tracksIds.Take(countToLoad).ToList();
            _currentOffset = countToLoad;

            return await _trackInfoProvider.GetTracksInfoByIds(idsToLoad);
        }
        catch (Exception exception)
        {
            exception.Handle();
            return [];
        }
    }
}
