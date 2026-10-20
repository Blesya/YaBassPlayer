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
			var configuration = new AppConfigurationProvider();
			var authService = new AuthService(
				new DeviceAuthorizationService(configuration),
				configuration);
			bool authorized = await TryAuthorizeAsync(authService);

			// Токена нет или он устарел — показываем окно входа с выбором способа.
			if (!authorized)
			{
				authorized = RunAuthWindow(authService, configuration);
			}

			if (!authorized)
			{
				Log.Information("Авторизация не выполнена, приложение будет закрыто");
				return;
			}

			ServicesProvider.Initialise(authService);
			Log.Information("Сервисы инициализированы");

			// UI поднимаем до инициализации BASS: при неудаче она показывает
			// MessageBox, а до Application.Init() драйвера ещё нет.
			Application.Init();
			Themes.InitializeDefaults();

			IAudioPlayer audioPlayer = ServicesProvider.Ioc.Resolve<IAudioPlayer>();
			audioPlayer.Init();

			View mainWindow = ServicesProvider.Ioc.Resolve<MainWindow>();
			Application.Top.Add(mainWindow);

			Themes.ApplySavedTheme();

			Log.Information("Запуск пользовательского интерфейса");
			Application.Run();

			// Если пользователь согласился на обновление, апдейтер стартует уже после закрытия UI:
			// подменять работающий exe и загруженные нативные DLL нельзя.
			ServicesProvider.Ioc.Resolve<IUpdateInstaller>().LaunchPending();
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

	/// <summary>
	/// Пытается войти без окна: сначала токеном из конфига, затем — тихо продлив его
	/// refresh-токеном (access-токен истёк или был отозван).
	/// </summary>
	private static async Task<bool> TryAuthorizeAsync(IAuthService authService)
	{
		if (AuthService.HasToken() && await authService.AuthorizeFromConfigAsync())
			return true;

		return AuthService.HasRefreshToken() && await authService.TryRefreshAsync();
	}

	/// <summary>Открывает окно входа с выбором способа (код на сайте, QR-код или токен).</summary>
	private static bool RunAuthWindow(IAuthService authService, IAppConfiguration configuration)
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
				configuration);

			return presenter.Run();
		}
		finally
		{
			Application.Shutdown();
		}
	}
}
