using System.Runtime.CompilerServices;
using Serilog;

namespace YamBassPlayer;

/// <summary>
/// Единая точка настройки Serilog. Логи пишутся в папку logs рядом с исполняемым файлом,
/// файл перекатывается раз в сутки.
/// </summary>
public static class Logging
{
	private const int RetainedFileCount = 30;

	public static string LogDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

	/// <summary>
	/// Пишет в лог начало вызова метода. Имя класса и метода подставляются автоматически.
	/// Вызывать только в ключевых, редко вызываемых методах — не в горячих путях
	/// (визуализация и отрисовка спектра, FFT, таймеры прогресса), иначе лог быстро разрастётся.
	/// </summary>
	public static void LogBeforeCall(
		[CallerMemberName] string methodName = "",
		[CallerFilePath] string filePath = "")
		=> Log.Debug("Начало вызова: {Class}.{Method}", GetClassName(filePath), methodName);

	/// <summary>
	/// Пишет в лог завершение вызова метода. Имя класса и метода подставляются автоматически.
	/// Вызывается в конце метода; при раннем выходе или исключении запись не появится.
	/// </summary>
	public static void LogAfterCall(
		[CallerMemberName] string methodName = "",
		[CallerFilePath] string filePath = "")
		=> Log.Debug("Завершение вызова: {Class}.{Method}", GetClassName(filePath), methodName);

	public static void Initialize()
	{
		try
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.Enrich.FromLogContext()
				.WriteTo.File(
					Path.Combine(LogDirectory, "yambassplayer-.log"),
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: RetainedFileCount,
					shared: true,
					flushToDiskInterval: TimeSpan.FromSeconds(1),
					outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
				.CreateLogger();

			RegisterGlobalExceptionHandlers();
		}
		catch (Exception exception)
		{
			// Логирование не должно мешать запуску плеера.
			Serilog.Debugging.SelfLog.Enable(Console.Error);
			Serilog.Debugging.SelfLog.WriteLine($"Не удалось настроить логирование: {exception}");
		}
	}

	public static void Close() => Log.CloseAndFlush();

	private static string GetClassName(string filePath) =>
		string.IsNullOrEmpty(filePath) ? "?" : Path.GetFileNameWithoutExtension(filePath);

	private static void RegisterGlobalExceptionHandlers()
	{
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			if (args.ExceptionObject is Exception exception)
			{
				Log.Fatal(exception, "Необработанное исключение");
			}
			else
			{
				Log.Fatal("Необработанное исключение: {ExceptionObject}", args.ExceptionObject);
			}

			// Если процесс завершается, буферизованные записи нужно успеть сбросить на диск.
			if (args.IsTerminating)
			{
				Log.CloseAndFlush();
			}
		};

		TaskScheduler.UnobservedTaskException += (_, args) =>
			Log.Error(args.Exception, "Необработанное исключение в фоновой задаче");
	}
}
