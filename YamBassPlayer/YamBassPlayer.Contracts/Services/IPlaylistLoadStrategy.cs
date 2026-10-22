using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Стратегия загрузки ядрового плейлиста (плейлиста без источника).
/// Вид плейлиста (<see cref="Playlist.Kind"/>) однозначно выбирает стратегию.
/// </summary>
public interface IPlaylistLoadStrategy
{
    /// <summary>Вид плейлиста, который обслуживает стратегия.</summary>
    string Kind { get; }

    /// <summary>Загружает ключи треков для заданного плейлиста.</summary>
    Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist);
}
