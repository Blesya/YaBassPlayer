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
		ErrorDialog.Show("Произошла непредвиденная ошибка", text);
	}
}
