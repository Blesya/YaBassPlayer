namespace YamBassPlayer.Models;

public class RecommendationResult(
	string sourceTrackId,
	IReadOnlyList<RecommendedTrack> tracks,
	bool insufficientData,
	bool usedArtistFallback)
{
	public string SourceTrackId { get; } = sourceTrackId;
	public IReadOnlyList<RecommendedTrack> Tracks { get; } = tracks;
	public bool InsufficientData { get; } = insufficientData;
	public bool UsedArtistFallback { get; } = usedArtistFallback;
}
