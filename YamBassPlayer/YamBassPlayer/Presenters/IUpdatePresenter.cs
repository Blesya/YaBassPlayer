namespace YamBassPlayer.Presenters;

/// <summary>
/// Сценарий обновления: проверить релиз, показать окно, скачать и запланировать установку.
/// </summary>
public interface IUpdatePresenter
{
	/// <summary>
	/// Запускает проверку в фоне. <paramref name="manual"/> — вызов из меню/команды:
	/// тогда результат показывается всегда, в том числе «у вас последняя версия».
	/// </summary>
	void CheckForUpdates(bool manual);
}
