using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Необязательная возможность источника: подробности о треке для окна «О треке».
/// Источник, который умеет отдавать больше, чем базовые поля <see cref="Track"/>,
/// реализует этот интерфейс; ядро спрашивает его через реестр источников.
/// </summary>
public interface ITrackAboutSource
{
	Task<TrackAboutInfo> GetAboutAsync(TrackKey key, bool forceRefresh = false);
}
