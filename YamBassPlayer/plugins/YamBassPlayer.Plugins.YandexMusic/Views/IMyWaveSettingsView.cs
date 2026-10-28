using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

/// <summary>
/// Окно настроек запуска «Моей волны»: треки, настроение, язык
/// и кнопки «Запустить» / «Отменить».
/// </summary>
public interface IMyWaveSettingsView
{
	event Action? OnStartClicked;
	event Action? OnCancelClicked;

	/// <summary>Отмечает сохранённый выбор в группах настроек.</summary>
	void SetSettings(MyWaveSettings settings);

	/// <summary>Выбор пользователя на момент нажатия «Запустить».</summary>
	MyWaveSettings SelectedSettings { get; }

	void Show();

	void Close();
}
