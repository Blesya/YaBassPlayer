using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackRepository
{
    /// <summary>
    /// Обновляет плейлисты всех источников и возвращает слитый список.
    /// Эквивалент <see cref="RefreshPlaylistsAsync"/> с областью <see cref="PlaylistRefreshScope.All"/>.
    /// </summary>
    Task<IEnumerable<Playlist>> GetPlaylists(CancellationToken ct = default);

    /// <summary>
    /// Снимок для первого кадра: сохранённые плейлисты (включая ветки ещё не готовых
    /// источников) и локально вычислимые плейлисты приложения. Сеть не используется.
    /// </summary>
    Task<PlaylistStartupSnapshot> GetStartupSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Обновляет плейлисты в фоне. Источники опрашиваются параллельно; ошибка одного
    /// источника оставляет его плейлисты из снимка и не затирает сохранённое состояние.
    /// </summary>
    Task<PlaylistRefreshResult> RefreshPlaylistsAsync(
        PlaylistRefreshScope scope,
        CancellationToken ct = default);

    /// <summary>
    /// Возвращает снимок состояния плейлистов, сохранённый предыдущим запуском.
    /// Позволяет показать прошлое дерево мгновенно, не дожидаясь ответа источника.
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

    /// <summary>
    /// Первые <paramref name="maxCount"/> треков текущего плейлиста, которые уже есть
    /// в локальной БД. В источник не ходит — список можно показать сразу.
    /// </summary>
    Task<IEnumerable<Track>> GetCachedTracks(int maxCount, CancellationToken ct = default);

    /// <summary>
    /// Догружает недостающие метаданные первых <paramref name="maxCount"/> треков из
    /// источника (сеть) и сохраняет их в БД. Возвращает всё, что удалось собрать.
    /// </summary>
    Task<IReadOnlyList<Track>> LoadTracksInfo(int maxCount, CancellationToken ct = default);
}
