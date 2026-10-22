using System.Threading;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackRepository
{
    Task<IEnumerable<Playlist>> GetPlaylists(CancellationToken ct = default);

    /// <summary>
    /// Возвращает снимок состояния плейлистов, сохранённый предыдущим запуском.
    /// Позволяет показать прошлое дерево мгновенно, не дожидаясь ответа источника.
    /// Состав плейлистов снимок не хранит — он кешируется только на время запуска.
    /// Возвращает null, если снимка нет.
    /// </summary>
    PlaylistState? GetPersistedSnapshot();
    Task SetPlaylist(Playlist playlist, CancellationToken ct = default);
    Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch, CancellationToken ct = default);

    IReadOnlyList<TrackKey> GetAllTrackIds();

    /// <summary>
    /// Запоминает трек, который начал играть, чтобы выделить его в списке
    /// при следующем запуске приложения.
    /// </summary>
    void SetLastPlayedTrack(TrackKey key);

    Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount, CancellationToken ct = default);
}
