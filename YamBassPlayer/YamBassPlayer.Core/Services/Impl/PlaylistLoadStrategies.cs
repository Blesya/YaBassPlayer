using Serilog;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class Top10LoadStrategy(IHistoryService historyService) : IPlaylistLoadStrategy
{
    public string Kind => "top10";
    public Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(historyService.GetTopTracks(10).Select(x => x.key).ToList());
}

public sealed class TopEveningsLoadStrategy(IHistoryService historyService) : IPlaylistLoadStrategy
{
    public string Kind => "top-evenings";
    public Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(historyService.GetTopEveningTracks(20).Select(x => x.key).ToList());
}

public sealed class TopByDayLoadStrategy(IHistoryService historyService) : IPlaylistLoadStrategy
{
    public string Kind => "top-by-day";

    public Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist)
    {
        string? dayName = playlist.Payload is { } payload && payload.TryGetValue("day", out string? value)
            ? value
            : null;

        if (!Enum.TryParse(dayName, ignoreCase: true, out DayOfWeek day))
        {
            Log.Warning("Плейлист {Title}: не удалось разобрать день недели «{Day}»", playlist.Title, dayName);
            return Task.FromResult(new List<TrackKey>());
        }

        return Task.FromResult(
            historyService.GetTopTracksByDayOfWeek(day, 50).Select(x => x.key).ToList());
    }
}

public sealed class CachedLoadStrategy(string tracksFolder) : IPlaylistLoadStrategy
{
    public string Kind => "cached";

    public Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist)
        => Task.Run(() =>
        {
            if (!Directory.Exists(tracksFolder))
                return new List<TrackKey>();

            var cached = Directory.GetDirectories(tracksFolder)
                .SelectMany(folder => Directory.GetFiles(folder, "*.mp3"))
                .Select(filePath => new FileInfo(filePath));

            // Плоский кэш старой версии: мигратор переносит его в tracks/yandex,
            // но если файл остался, «Загруженные» не должны его терять.
            var legacy = Directory.GetFiles(tracksFolder, "*.mp3", SearchOption.TopDirectoryOnly)
                .Select(filePath => new FileInfo(filePath));

            return cached
                .Concat(legacy)
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .Select(fileInfo => new TrackKey(
                    string.Equals(fileInfo.Directory!.FullName, Path.GetFullPath(tracksFolder), StringComparison.OrdinalIgnoreCase)
                        ? new SourceId("yandex")
                        : new SourceId(fileInfo.Directory.Name),
                    Path.GetFileNameWithoutExtension(fileInfo.Name)))
                .Where(key => !string.IsNullOrEmpty(key.TrackId))
                .ToList();
        });
}

public sealed class ArtistLoadStrategy(ITrackInfoProvider trackInfoProvider) : IPlaylistLoadStrategy
{
    public string Kind => "artist";

    public async Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist)
        => (await trackInfoProvider.GetTrackIdsByArtistAsync(playlist.Title)).ToList();
}

public sealed class QueueLoadStrategy(ITrackRepositoryCache cache) : IPlaylistLoadStrategy
{
    public string Kind => "queue";
    public Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist)
        => Task.FromResult(cache.QueueTrackIds.ToList());
}

/// <summary>Локальное избранное: ключи любых источников, которые выбрал пользователь.</summary>
public sealed class FavoriteLoadStrategy(ILocalFavoriteService favorites) : IPlaylistLoadStrategy
{
    public string Kind => "favorite";

    public async Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist)
        => (await favorites.GetAllFavoriteKeys()).ToList();
}
