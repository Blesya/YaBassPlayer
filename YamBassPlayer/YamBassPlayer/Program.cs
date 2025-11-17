using Terminal.Gui;
using YamBassPlayer.Views;

namespace YamBassPlayer
{
	internal class Program
	{
		private static async Task Main(string[] args)
		{
			AudioPlayer.Init();

			Application.Init();

			var mainWindow = new MainWindow();

			Application.Top.Add(mainWindow);

			Application.Run();
		}
	}
}
