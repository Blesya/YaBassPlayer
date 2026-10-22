namespace YamBassPlayer.Models;

/// <summary>
/// Прогресс загрузки трека: сколько байт уже получено и полный размер (0, если неизвестен).
/// </summary>
public readonly record struct DownloadProgress(long BytesReceived, long TotalBytes)
{
	public bool HasTotal => TotalBytes > 0;

	/// <summary>Процент загрузки (0-100). Равен 0, если полный размер неизвестен.</summary>
	public int Percent => HasTotal
		? (int)Math.Min(BytesReceived * 100 / TotalBytes, 100)
		: 0;
}
