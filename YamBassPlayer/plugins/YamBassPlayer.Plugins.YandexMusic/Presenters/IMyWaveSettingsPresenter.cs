namespace YamBassPlayer.Presenters;

/// <summary>
/// Сценарий «Моя волна» из меню плагина: окно настроек, применение
/// настроек к станции и запуск волны через хост.
/// </summary>
public interface IMyWaveSettingsPresenter
{
	Task ShowAsync();
}
