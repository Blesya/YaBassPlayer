using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IRecommendationService
{
	Task<RecommendationResult> GetRecommendationsAsync(string currentTrackId, int limit = 20);
	Task<GraphData> GetGraphDataAsync(string centerTrackId, int depth = 2, int maxEdgesPerNode = 5);
}
