using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Статистика прослушиваний для окна «Статистика»: время, артисты, ритуалы, рекорды.
/// Отделена от <see cref="IDatabaseStatisticsService"/>: тот считает размеры базы и кэша.
/// </summary>
public interface IListeningStatisticsService
{
	ListeningStatistics Collect();
}
