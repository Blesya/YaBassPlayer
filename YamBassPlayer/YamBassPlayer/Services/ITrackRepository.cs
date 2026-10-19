using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackRepository
{
    Task<IEnumerable<Playlist>> GetPlaylists(CancellationToken ct = default);

    /// <summary>
    /// Возвращает снимок состояния плейлистов, сохранённый предыдущим запуском,
    /// и наполняет внутренние кэши состава плейлистов из него. Позволяет показать
    /// прошлое состояние мгновенно, не дожидаясь ответа источника. Возвращает null,
    /// если снимка нет.
    /// </summary>
    PlaylistState? GetPersistedSnapshot();
    Task SetPlaylist(Playlist playlist, CancellationToken ct = default);
    Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch, CancellationToken ct = default);

    IReadOnlyList<string> GetAllTrackIds();
    PlaylistType? CurrentPlaylistType { get; }

    /// <summary>
    /// Запоминает трек, который начал играть, чтобы выделить его в списке
    /// при следующем запуске приложения.
    /// </summary>
    void SetLastPlayedTrack(string trackId);

    Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount, CancellationToken ct = default);
}
