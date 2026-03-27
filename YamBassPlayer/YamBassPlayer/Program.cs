using Autofac;
using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Extensions;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer;

internal class Program
{
	private static async Task Main(string[] args)
	{
		try
		{
			if (!AuthService.HasToken())
			{
				Application.Init();
				Themes.InitializeDefaults();
					
				var tokenDialog = new TokenInputDialog();
				Application.Run(tokenDialog);

				if (tokenDialog.Cancelled || string.IsNullOrWhiteSpace(tokenDialog.Token))
				{
					Application.Shutdown();
					return;
				}

				AppConfiguration.SaveToken(tokenDialog.Token);
				Application.Shutdown();
			}

			var authService = new AuthService();
			bool authorized = await authService.AuthorizeFromConfigAsync();

			if (!authorized)
			{
				Application.Init();
				MessageBox.ErrorQuery("Ошибка авторизации", 
					"Не удалось авторизоваться. Проверьте токен.", "OK");
				Application.Shutdown();
				return;
			}

			ServicesProvider.Initialise(authService);

			IAudioPlayer audioPlayer = ServicesProvider.Ioc.Resolve<IAudioPlayer>();
			audioPlayer.Init();

			Application.Init();
			Themes.InitializeDefaults();
				
			View mainWindow = ServicesProvider.Ioc.Resolve<MainWindow>();
			Application.Top.Add(mainWindow);

			Themes.ApplySavedTheme();

			Application.Run();
		}
		catch (Exception exception)
		{
			exception.Handle();
		}
	}
}