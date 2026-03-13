namespace YamBassPlayer.Models;

public class RecommendedTrack(string trackId, double score, RecommendationReason reason)
{
	public string TrackId { get; } = trackId;
	public double Score { get; } = score;
	public RecommendationReason Reason { get; } = reason;
}
