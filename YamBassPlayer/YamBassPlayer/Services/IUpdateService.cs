using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Проверка обновлений и загрузка архива. Сеть живёт здесь, UI — в презентере.
/// </summary>
public interface IUpdateService
{
	/// <summary>Последний доступный релиз либо <c>null</c>, если обновляться не на что.</summary>
	Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken);

	/// <summary>Скачивает архив во временный каталог и проверяет sha256. Возвращает путь к zip.</summary>
	Task<string> DownloadAsync(UpdateInfo info, IProgress<int>? progress, CancellationToken cancellationToken);
}
