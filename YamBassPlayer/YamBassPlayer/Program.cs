using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Views;

namespace YamBassPlayer
{
	internal class Program
	{
		private static Task Main(string[] args)
		{
			try
			{
				AudioPlayer.Init();

                Application.Init();

				Themes.InitializeDefaults();

                var mainWindow = new MainWindow();

				Application.Top.Add(mainWindow);

                Application.Run();
            }
			catch (Exception exception)
			{
				exception.Handle();
			}

			return Task.CompletedTask;
		}
	}
}
