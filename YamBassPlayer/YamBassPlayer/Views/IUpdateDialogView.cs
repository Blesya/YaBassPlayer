using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

/// <summary>Что пользователь решил в окне обновления.</summary>
public enum UpdateDecision
{
	Postpone,
	Install
}

/// <summary>Результат окна обновления: отложить или ставить (с путём к скачанному архиву).</summary>
public sealed record UpdateDialogResult(UpdateDecision Decision, string? ZipPath);

/// <summary>
/// Окно «Найдена новая версия». Одно окно на весь сценарий: кнопка «Загрузить»
/// после скачивания превращается в «Установить».
/// </summary>
public interface IUpdateDialogView
{
	/// <summary>
	/// Показывает окно и блокирует UI до решения пользователя.
	/// <paramref name="download"/> — операция скачивания с прогрессом и отменой.
	/// </summary>
	UpdateDialogResult Show(
		UpdateInfo info,
		string currentVersion,
		Func<IProgress<int>, CancellationToken, Task<string>> download);

	/// <summary>Сообщает, что обновляться некуда (ручная проверка).</summary>
	void ShowUpToDate(string currentVersion);

	/// <summary>Показывает ошибку проверки или скачивания.</summary>
	void ShowError(string message);
}
