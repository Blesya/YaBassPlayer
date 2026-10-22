namespace YamBassPlayer.Models;

/// <summary>
/// Трек недоступен и у него нет замены. Плеер не показывает общий диалог ошибки,
/// а открывает «О треке» с причиной.
/// </summary>
public sealed class TrackUnavailableException(string trackId)
	: Exception($"Трек недоступен: {trackId}")
{
	public string TrackId { get; } = trackId;
}
