using Autofac;
using Serilog;
using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Constants;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.TerminalOutput;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer;

internal class Program
{
	private static async Task Main(string[] args)
	{
		Logging.Initialize();
		Log.Information("Запуск YamBassPlayer. Каталог: {BaseDirectory}", AppDomain.CurrentDomain.BaseDirectory);
		SetWindowTitle();

		var consoleWindow = new ConsoleWindow();
		bool uiInitialized = false;

		try
		{
			RestoreWindowSize(consoleWindow);
			RestoreWindowPosition(consoleWindow);

			var configuration = new AppConfigurationProvider();

			// UI поднимаем до инициализации BASS и плагинов: при неудаче показывается
			// MessageBox, а до Application.Init() драйвера ещё нет.
			// Если терминал умеет VT — берём ANSI-драйвер с TrueColor-обложками,
			// иначе Terminal.Gui поднимет штатный (16 цветов).
			TrueColorDriverFactory.TryCreate(out ConsoleDriver? driver, out IMainLoopDriver? mainLoopDriver);
			Application.Init(driver, mainLoopDriver);
			uiInitialized = true;
			Themes.InitializeDefaults();

			// Авторизация больше не блокирует запуск: источники без токена
			// показывают состояние «требуется настройка».
			ServicesProvider.Initialise();
			Log.Information("Сервисы инициализированы");

			PluginLoader pluginLoader = ServicesProvider.Ioc.Resolve<PluginLoader>();
			pluginLoader.LoadAll(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"));
			Log.Information("Плагины загружены: {Count}", pluginLoader.LoadedPlugins.Count);

			// Подписка до InitializeAsync: иначе вход в фоне (сохранённый токен)
			// завершится раньше, чем дерево успеет узнать об этом.
			IPlaylistsPresenter playlistsPresenter = ServicesProvider.Ioc.Resolve<IPlaylistsPresenter>();
			foreach (IPlugin plugin in pluginLoader.LoadedPlugins)
				plugin.Changed += () => playlistsPresenter.LoadPlaylistTree();

			// Локальное избранное живёт в дереве: после сердечка его состав меняется.
			// Сеть для этого не нужна — перечитываем только локальные плейлисты.
			ILocalFavoriteService favorites = ServicesProvider.Ioc.Resolve<ILocalFavoriteService>();
			favorites.Changed += () => playlistsPresenter.RefreshAppPlaylists();

			foreach (IPlugin plugin in pluginLoader.LoadedPlugins)
				_ = Task.Run(() => plugin.InitializeAsync(CancellationToken.None));

			IAudioPlayer audioPlayer = ServicesProvider.Ioc.Resolve<IAudioPlayer>();
			audioPlayer.Init();

			View mainWindow = ServicesProvider.Ioc.Resolve<MainWindow>();
			Application.Top.Add(mainWindow);
			TrackWindowSizeChanges(consoleWindow);
			TrackWindowPositionChanges(consoleWindow);

			Themes.ApplySavedTheme();

			Log.Information("Запуск пользовательского интерфейса");
			Application.Run();

			// Обязательно: драйвер возвращает терминал в нормальное состояние
			// (альтернативный экран, курсор, режимы консоли).
			ShutdownUi(ref uiInitialized);

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
			// Если исключение случилось до Application.Run(), драйвер всё равно
			// нужно корректно погасить, иначе терминал останется в alt-screen.
			ShutdownUi(ref uiInitialized);

			SaveWindowPosition(consoleWindow);
			SaveWindowSize(consoleWindow);
			Log.Information("Завершение работы YamBassPlayer");
			Logging.Close();
		}
	}

	/// <summary>
	/// Завершает Terminal.Gui ровно один раз: возвращает терминал в нормальное
	/// состояние (альтернативный экран, курсор, режимы консоли).
	/// </summary>
	private static void ShutdownUi(ref bool uiInitialized)
	{
		if (!uiInitialized)
			return;

		uiInitialized = false;

		try
		{
			Application.Shutdown();
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Не удалось корректно завершить Terminal.Gui");
		}
	}

	private static readonly TimeSpan WindowSizeSaveInterval = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Заголовок терминального окна на старте: имя приложения и версия. Когда
	/// начнётся воспроизведение, его сменит исполнитель и название трека.
	/// </summary>
	private static void SetWindowTitle()
	{
		try
		{
			Console.Title = AppInfo.WindowTitle;
		}
		catch (Exception exception)
		{
			// Без консоли (вывод перенаправлен) заголовок ставить некуда — не повод падать.
			Log.Debug(exception, "Не удалось установить заголовок терминального окна");
		}
	}

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
	/// Восстанавливает сохранённое положение окна. Если монитор с этой точкой
	/// отключили, окно остаётся там, куда его поставил терминал.
	/// </summary>
	private static void RestoreWindowPosition(IConsoleWindow consoleWindow)
	{
		if (AppConfiguration.GetWindowPosition() is not { } saved)
			return;

		if (consoleWindow.TrySetPosition(saved))
			Log.Information("Положение окна восстановлено: {X},{Y}", saved.X, saved.Y);
		else
			Log.Information("Не удалось восстановить положение окна {X},{Y}", saved.X, saved.Y);
	}

	/// <summary>
	/// Terminal.Gui не сообщает о перемещении окна, поэтому проверяем положение
	/// раз в секунду: так оно сохранится, даже если окно закрыть крестиком.
	/// </summary>
	private static void TrackWindowPositionChanges(IConsoleWindow consoleWindow)
	{
		Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), _ =>
		{
			SaveWindowPosition(consoleWindow);
			return true;
		});
	}

	/// <summary>Сохраняет положение окна в конфиг, если оно отличается от сохранённого.</summary>
	private static void SaveWindowPosition(IConsoleWindow consoleWindow)
	{
		ConsoleWindowPosition? current = consoleWindow.GetPosition();
		if (current is null || current == AppConfiguration.GetWindowPosition())
			return;

		AppConfiguration.SaveWindowPosition(current.Value.X, current.Value.Y);
		Log.Information("Положение окна сохранено: {X},{Y}", current.Value.X, current.Value.Y);
	}

}
