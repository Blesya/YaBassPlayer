using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IDatabaseStatisticsService
{
	DatabaseStatistics CollectStatistics();
}
