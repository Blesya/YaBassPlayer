using Autofac;
using Serilog;
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
		Logging.Initialize();
		Log.Information("Запуск YamBassPlayer. Каталог: {BaseDirectory}", AppDomain.CurrentDomain.BaseDirectory);

		try
		{
			if (!AuthService.HasToken())
			{
				Log.Information("Токен не найден, запрашиваем у пользователя");
				Application.Init();
				Themes.InitializeDefaults();
					
				var tokenDialog = new TokenInputDialog();
				Application.Run(tokenDialog);

				if (tokenDialog.Cancelled || string.IsNullOrWhiteSpace(tokenDialog.Token))
				{
					Log.Information("Ввод токена отменён пользователем");
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
				Log.Warning("Авторизация не удалась, приложение будет закрыто");
				Application.Init();
				MessageBox.ErrorQuery("Ошибка авторизации", 
					"Не удалось авторизоваться. Проверьте токен.", "OK");
				Application.Shutdown();
				return;
			}

			ServicesProvider.Initialise(authService);
			Log.Information("Сервисы инициализированы");

			IAudioPlayer audioPlayer = ServicesProvider.Ioc.Resolve<IAudioPlayer>();
			audioPlayer.Init();

			Application.Init();
			Themes.InitializeDefaults();
				
			View mainWindow = ServicesProvider.Ioc.Resolve<MainWindow>();
			Application.Top.Add(mainWindow);

			Themes.ApplySavedTheme();

			Log.Information("Запуск пользовательского интерфейса");
			Application.Run();
		}
		catch (Exception exception)
		{
			Log.Fatal(exception, "Необработанное исключение при работе приложения");
			exception.Handle(logException: false);
		}
		finally
		{
			Log.Information("Завершение работы YamBassPlayer");
			Logging.Close();
		}
	}
}