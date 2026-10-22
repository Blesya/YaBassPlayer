namespace YamBassPlayer.Services;

/// <summary>
/// Результат рекомендации следующего трека.
/// <see cref="TrackId"/> заполнен при успехе; иначе в <see cref="Message"/>
/// содержится текст о причине, почему модель не смогла рекомендовать.
/// </summary>
public sealed class NextTrackRecommendation
{
	public string? TrackId { get; init; }
	public string Message { get; init; } = "";

	public bool IsSuccess => !string.IsNullOrEmpty(TrackId);
}
