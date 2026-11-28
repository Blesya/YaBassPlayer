namespace YamBassPlayer.Models;

/// <summary>
/// Рекомендация ListenBrainz, сопоставленная с нашей библиотекой.
/// <see cref="Key"/> заполнен, только если трек нашёлся у источника:
/// остальные показывать бессмысленно — их нечем играть.
/// </summary>
public sealed record RecommendationTrack(
	string Mbid,
	string Artist,
	string Title,
	string? Album,
	int? DurationMs,
	double Score,
	TrackKey? Key)
{
	public bool IsResolved => Key is not null;
}
