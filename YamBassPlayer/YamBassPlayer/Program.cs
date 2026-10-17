using Autofac;
using Serilog;
using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Extensions;
using YamBassPlayer.Presenters.Impl;
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
			var authService = new AuthService();
			bool authorized = await TryAuthorizeAsync(authService);

			// Токена нет или он устарел — показываем окно входа с выбором способа.
			if (!authorized)
			{
				authorized = RunAuthWindow(authService);
			}

			if (!authorized)
			{
				Log.Information("Авторизация не выполнена, приложение будет закрыто");
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

	/// <summary>Пытается войти по токену из конфига, не открывая окно.</summary>
	private static async Task<bool> TryAuthorizeAsync(IAuthService authService)
	{
		if (!AuthService.HasToken())
			return false;

		return await authService.AuthorizeFromConfigAsync();
	}

	/// <summary>Открывает окно входа с выбором способа (QR-код или токен).</summary>
	private static bool RunAuthWindow(IAuthService authService)
	{
		Log.Information("Требуется вход, открываем окно авторизации");
		Application.Init();
		Themes.InitializeDefaults();

		try
		{
			var presenter = new AuthPresenter(
				authService,
				new QrCodeRenderer(),
				new AuthView(),
				new UiDispatcher(),
				new UiErrorHandler(),
				new AppConfigurationProvider());

			return presenter.Run();
		}
		finally
		{
			Application.Shutdown();
		}
	}
}
