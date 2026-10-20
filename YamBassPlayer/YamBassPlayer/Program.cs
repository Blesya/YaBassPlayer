using Autofac;
using Serilog;
using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
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

		var consoleWindow = new ConsoleWindow();

		try
		{
			RestoreWindowSize(consoleWindow);

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
			TrackWindowSizeChanges(consoleWindow);

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
			SaveWindowSize(consoleWindow);
			Log.Information("Завершение работы YamBassPlayer");
			Logging.Close();
		}
	}

	private static readonly TimeSpan WindowSizeSaveInterval = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Восстанавливает сохранённый размер терминального окна. Терминал может
	/// не поддержать изменение размера — тогда просто продолжаем с текущим.
	/// </summary>
	private static void RestoreWindowSize(IConsoleWindow consoleWindow)
	{
		if (AppConfiguration.GetWindowSize() is not { } saved)
			return;

		if (consoleWindow.TrySetSize(saved))
			Log.Information("Размер окна восстановлен: {Width}x{Height}", saved.Width, saved.Height);
		else
			Log.Information("Терминал не позволил восстановить размер окна {Width}x{Height}", saved.Width, saved.Height);
	}

	/// <summary>
	/// Сохраняет размер окна при перетаскивании: событие приходит десятки раз,
	/// поэтому конфиг пишется не чаще раза в секунду. Итоговый размер пишется при выходе.
	/// </summary>
	private static void TrackWindowSizeChanges(IConsoleWindow consoleWindow)
	{
		DateTime lastSave = DateTime.MinValue;

		Application.Top.Resized += size =>
		{
			DateTime now = DateTime.UtcNow;
			if (now - lastSave < WindowSizeSaveInterval)
				return;

			lastSave = now;
			SaveWindowSize(consoleWindow, new ConsoleWindowSize(size.Width, size.Height));
		};
	}

	/// <summary>Сохраняет размер окна в конфиг, если он отличается от сохранённого.</summary>
	private static void SaveWindowSize(IConsoleWindow consoleWindow, ConsoleWindowSize? size = null)
	{
		ConsoleWindowSize? current = size ?? consoleWindow.GetSize();
		if (current is null || current == AppConfiguration.GetWindowSize())
			return;

		AppConfiguration.SaveWindowSize(current.Value.Width, current.Value.Height);
		Log.Information("Размер окна сохранён: {Width}x{Height}", current.Value.Width, current.Value.Height);
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
