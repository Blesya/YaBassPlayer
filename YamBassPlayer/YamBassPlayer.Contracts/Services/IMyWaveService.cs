namespace YamBassPlayer.Services;

/// <summary>
/// Сценарий «Моя волна», доступный плагинам: ядро само решает,
/// как показать плейлист и переключить воспроизведение.
/// </summary>
public interface IMyWaveService
{
	Task ShowAsync(Action<string> setWindowTitle);
	Task ShowByTrackAsync(Action<string> setWindowTitle);
}
