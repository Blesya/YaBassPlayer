using Serilog;
using Terminal.Gui;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Extensions;

public static class ExceptionExtensions
{
	/// <summary>
	/// Показывает диалог с самым вложенным исключением и по умолчанию пишет исключение в лог.
	/// </summary>
	public static void Handle(this Exception exception, bool logException = true)
	{
		if (logException)
		{
			Log.Error(exception, "Произошла непредвиденная ошибка");
		}

		var innermost = exception;
		while (innermost?.InnerException != null)
		{
			innermost = innermost.InnerException;
		}

		var message = innermost?.Message ?? "Ошибка";
		var stackTrace = innermost?.StackTrace ?? "Стектрейс отсутствует!";
		var text = $"{message}\n\n{stackTrace}";
		ShowDialog("Произошла непредвиденная ошибка", text);
	}

	/// <summary>
	/// Показывает диалог в UI-потоке. <see cref="Handle"/> вызывается из catch после
	/// await, то есть уже в потоке пула, а Terminal.Gui не потокобезопасен: без
	/// маршалинга вложенный цикл <c>Application.Run</c> запускается не из того потока.
	/// </summary>
	private static void ShowDialog(string title, string text)
	{
		if (Application.MainLoop is { } mainLoop)
		{
			mainLoop.Invoke(() => ErrorDialog.Show(title, text));
			return;
		}

		ErrorDialog.Show(title, text);
	}
}
