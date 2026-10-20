using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Источник сведений о последнем релизе. Отдельная абстракция, чтобы сценарий
/// обновления не зависел от HTTP и формата ответа GitHub.
/// </summary>
public interface IReleaseFeed
{
	/// <summary>Возвращает последний релиз либо <c>null</c>, если подходящего ассета нет.</summary>
	Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken);
}
