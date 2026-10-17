namespace YamBassPlayer.Models;

/// <summary>Что сейчас происходит с треком в локальном кэше.</summary>
public enum TrackDownloadStatus
{
	/// <summary>Ничего не происходит.</summary>
	Idle,

	/// <summary>Трек качается прямо сейчас.</summary>
	Downloading,

	/// <summary>Трек лежит в локальном кэше.</summary>
	Cached,

	/// <summary>Загрузка не удалась.</summary>
	Failed
}

/// <summary>
/// Состояние трека в локальном кэше: статус и, пока идёт загрузка, прогресс.
/// Одна и та же структура описывает и «уже скачан», и «качается на 45%», и «ошибка».
/// </summary>
public readonly record struct TrackDownloadState(
	TrackDownloadStatus Status,
	int Percent,
	long BytesReceived,
	bool HasTotal)
{
	public static readonly TrackDownloadState Idle = new(TrackDownloadStatus.Idle, 0, 0, false);
	public static readonly TrackDownloadState Cached = new(TrackDownloadStatus.Cached, 100, 0, false);
	public static readonly TrackDownloadState Failed = new(TrackDownloadStatus.Failed, 0, 0, false);

	public bool IsDownloading => Status == TrackDownloadStatus.Downloading;
	public bool IsCached => Status == TrackDownloadStatus.Cached;

	public static TrackDownloadState Downloading(DownloadProgress progress)
		=> new(TrackDownloadStatus.Downloading, progress.Percent, progress.BytesReceived, progress.HasTotal);
}
