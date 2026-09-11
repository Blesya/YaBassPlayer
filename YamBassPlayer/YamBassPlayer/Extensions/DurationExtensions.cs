namespace YamBassPlayer.Extensions;

public static class DurationExtensions
{
	/// <summary>
	/// Форматирует длительность в компактный вид «m:ss» (для треков длиннее часа — «h:mm:ss»),
	/// например 225000 мс → «3:45». Для отсутствующей или неположительной длительности
	/// возвращает пустую строку.
	/// </summary>
	public static string ToShortDuration(this long? milliseconds)
	{
		if (milliseconds is not { } ms || ms <= 0)
			return string.Empty;

		// Защита от переполнения TimeSpan на экстремальных значениях.
		double safeMs = Math.Min((double)ms, TimeSpan.MaxValue.TotalMilliseconds);
		var duration = TimeSpan.FromMilliseconds(safeMs);
		return duration.TotalHours >= 1
			? duration.ToString(@"h\:mm\:ss")
			: duration.ToString(@"m\:ss");
	}
}
