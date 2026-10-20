using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Открывает окно обновления и служебные сообщения проверки. Отдельный класс,
/// чтобы презентер не зависел от Terminal.Gui.
/// </summary>
public sealed class UpdateDialogView : IUpdateDialogView
{
	public UpdateDialogResult Show(
		UpdateInfo info,
		string currentVersion,
		Func<IProgress<int>, CancellationToken, Task<string>> download)
	{
		var dialog = new UpdateDialog(info, currentVersion, download);
		Application.Run(dialog);
		return dialog.Result;
	}

	public void ShowUpToDate(string currentVersion)
	{
		MessageBox.Query("Обновление", $"У вас последняя версия ({currentVersion}).", "OK");
	}

	public void ShowError(string message)
	{
		MessageBox.Query("Обновление", message, "OK");
	}
}
