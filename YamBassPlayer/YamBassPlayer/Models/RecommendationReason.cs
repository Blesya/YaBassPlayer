namespace YamBassPlayer.Models;

public enum RecommendationReasonType
{
	DirectTransition,
	ReverseTransition,
	ArtistTransition,
	ArtistLevelTransition
}

public record RecommendationReason(RecommendationReasonType Type, int Count)
{
	public string ToDisplayString() => Type switch
	{
		RecommendationReasonType.DirectTransition => $"после этого трека × {Count}",
		RecommendationReasonType.ReverseTransition => $"перед этим треком × {Count}",
		RecommendationReasonType.ArtistTransition => $"от исполнителя × {Count}",
		RecommendationReasonType.ArtistLevelTransition => $"к исполнителю × {Count}",
		_ => ""
	};
}
