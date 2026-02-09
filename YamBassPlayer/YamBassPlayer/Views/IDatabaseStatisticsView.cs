using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IDatabaseStatisticsView
{
	void SetStatistics(DatabaseStatistics stats);
	void Show();
	void Close();
}
