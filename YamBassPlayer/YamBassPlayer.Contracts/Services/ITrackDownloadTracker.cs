using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Единый источник правды о том, что сейчас качается в локальный кэш.
/// Загрузка сообщает сюда о старте, прогрессе и исходе, а список треков
/// подписывается на <see cref="Changed"/> и рисует прогресс прямо в строке.
/// </summary>
public interface ITrackDownloadTracker
{
	/// <summary>Состояние загрузки изменилось: ключ трека и новое состояние.</summary>
	event Action<TrackKey, TrackDownloadState>? Changed;

	/// <summary>Загрузка трека началась.</summary>
	void MarkDownloading(TrackKey key);

	/// <summary>Очередной отчёт о полученных байтах.</summary>
	void ReportProgress(TrackKey key, DownloadProgress progress);

	/// <summary>Трек лёг в кэш.</summary>
	void MarkCached(TrackKey key);

	/// <summary>Загрузка не удалась.</summary>
	void MarkFailed(TrackKey key);

	/// <summary>
	/// Загрузки, которые ещё идут или закончились ошибкой. Нужны, чтобы только что
	/// построенный список сразу показал прогресс, а не ждал следующего отчёта.
	/// </summary>
	IReadOnlyList<(TrackKey Key, TrackDownloadState State)> GetActive();
}
