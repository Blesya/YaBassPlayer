using Terminal.Gui;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Extensions;

public static class ExceptionExtensions
{
	public static void Handle(this Exception exception)
	{
		if (exception?.InnerException != null)
		{
			exception.InnerException.Handle();
			return;
		}

		var message = exception?.Message ?? "Ошибка";
		var stackTrace = exception?.StackTrace ?? "Стектрейс отсутствует!";
		var text = $"{message}\n\n{stackTrace}";
		ErrorDialog.Show("Произошла непредвиденная ошибка", text);
	}
}