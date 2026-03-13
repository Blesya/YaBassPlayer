using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IRecommendationGraphView
{
	void SetGraphData(GraphData data);
	void Show();
	void Close();
}
