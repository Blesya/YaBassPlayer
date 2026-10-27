using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Состояние последнего обновления источника. Дерево помечает ветку источника
/// «кеш», если обновление не удалось, а показанные плейлисты взяты из снимка.
/// </summary>
public interface IPlaylistRefreshStatus
{
	void ReportSuccess(SourceId source, DateTime utc);

	void ReportFailure(SourceId source, string error, DateTime utc);

	/// <summary>Последнее обновление источника завершилось ошибкой.</summary>
	bool IsStale(SourceId source);

	DateTime? GetLastSuccess(SourceId source);
}
