using Terminal.Gui;

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

	    MessageBox.ErrorQuery(exception?.Message, exception?.StackTrace ?? "Стектрейс отсутствует!", "OK");
	}
}