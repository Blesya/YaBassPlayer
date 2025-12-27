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

public class TrackRepository(
    YandexMusicApi api,
    AuthStorage storage,
    ITrackInfoProvider trackInfoProvider,
    string tracksFolder,
    IHistoryService historyService,
    ILocalFavoriteService localFavoriteService)
    : ITrackRepository
{
    private List<string> _tracksIds = new();
    private Playlist _currentPlaylist;
    private int _currentOffset = 0;
    private readonly Dictionary<string, List<string>> _customPlaylistCache = new();
    private readonly List<string> _favoritePlaylistCache = new();
    private readonly List<string> _localSearchCache = new();

    public async Task<IEnumerable<Playlist>> GetPlaylists()
    {
        try
        {
            IEnumerable<YResponse<YPlaylist>>? yResponses = await api.Playlist.GetPersonalPlaylistsAsync(storage);

            YResponse<YLibraryTracks>? liked = await api.Library.GetLikedTracksAsync(storage);
            string[] favoriteTrackIds = liked.Result.Library.Tracks.Select(x => x.Id).ToArray();
            _favoritePlaylistCache.AddRange(favoriteTrackIds);
            int likedTracksCount = favoriteTrackIds.Length;

            var localFavoriteIds = await localFavoriteService.GetAllFavoriteTrackIds();

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
                },
                new Playlist("Топ вечеров", PlaylistType.TopEvenings)
                {
                    Description = "Топ треков с 16:00 до 24:00",
                    TrackCount = 20
                },
            ];

            foreach (YResponse<YPlaylist> yResponse in yResponses)
            {
                List<YTrackContainer>? yTrackContainers = yResponse.Result.Tracks;
                IEnumerable<Track> tracks = yTrackContainers.Select(yTrackContainer => yTrackContainer.Track.ToTrack());
                foreach (Track track in tracks)
                {
                    await trackInfoProvider.SaveAsync(track);
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
                case PlaylistType.TopEvenings:
                    await SetTopEveningsPlaylist(playlist);
                    break;
                case PlaylistType.LocalFavorite:
                    await SetLocalFavoritePlaylist(playlist);
                    break;
                case PlaylistType.LocalSearch:
                    await SetLocalSearchPlaylist(playlist);
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
            List<string> day = historyService.GetTopTracks(10)
                .Select(x => x.trackId)
                .ToList();
            return Task.FromResult(day);
        });

    private Task SetTopEveningsPlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadTopEveningsPlaylist);

    private Task<List<string>> LoadTopEveningsPlaylist()
        => Task.Run(() =>
        {
            List<string> evenings = historyService.GetTopEveningTracks(20)
                .Select(x => x.trackId)
                .ToList();
            return Task.FromResult(evenings);
        });

    private Task SetCustomPlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, () => LoadCustomPlaylistAsync(playlist.PlaylistName));

    private Task SetCachedPlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadCachedTracksAsync);

    private Task SetFavorite(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadFavoritesAsync);

    private Task SetLocalFavoritePlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadLocalFavoritesAsync);

    private Task SetLocalSearchPlaylist(Playlist playlist)
        => SetPlaylistAsync(playlist, LoadLocalSearchAsync);

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

                var playlists = await api.Playlist.GetPersonalPlaylistsAsync(storage);
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
        return await localFavoriteService.GetAllFavoriteTrackIds();
    }

    private Task<List<string>> LoadLocalSearchAsync()
    {
        return Task.FromResult(_localSearchCache.ToList());
    }

    private Task<List<string>> LoadPlaylistOfTheDayAsync()
        => Task.Run(async () =>
        {
            var day = await api.Playlist.OfTheDayAsync(storage);
            return day.Result.Tracks.Select(t => t.Id).ToList();
        });

    private Task<List<string>> LoadCachedTracksAsync()
        => Task.Run(() =>
        {
            if (!Directory.Exists(tracksFolder))
            {
                return new List<string>();
            }

            return Directory.GetFiles(tracksFolder, "*.mp3")
                .Select(filePath => new FileInfo(filePath))
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList()!;
        });

    public int GetCachedTracksCount()
    {
        if (!Directory.Exists(tracksFolder))
        {
            return 0;
        }

        return Directory.GetFiles(tracksFolder, "*.mp3").Length;
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

            IEnumerable<Track> tracks = await trackInfoProvider.GetTracksInfoByIds(slice);

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

    public void UpdateLocalSearchCache(IEnumerable<Track> tracks)
    {
        _localSearchCache.Clear();
        _localSearchCache.AddRange(tracks.Select(t => t.Id));
    }

    public async Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount)
    {
        try
        {
            int cachedCount = await trackInfoProvider.CountCachedTracks(_tracksIds);

            int countToLoad = Math.Max(cachedCount, minCount);
            countToLoad = Math.Min(countToLoad, _tracksIds.Count);

            var idsToLoad = _tracksIds.Take(countToLoad).ToList();
            _currentOffset = countToLoad;

            return await trackInfoProvider.GetTracksInfoByIds(idsToLoad);
        }
        catch (Exception exception)
        {
            exception.Handle();
            return [];
        }
    }
}
