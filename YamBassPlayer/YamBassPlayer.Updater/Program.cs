using System.Diagnostics;

namespace YamBassPlayer.Updater;

/// <summary>
/// Применяет скачанное обновление к каталогу установки. Запускается приложением,
/// которое уже закрылось (или закрывается прямо сейчас), поэтому первым делом ждёт
/// завершения родительского процесса: пока он жив, его exe и нативные DLL заблокированы.
/// </summary>
internal static class Program
{
	private const int ExitSuccess = 0;
	private const int ExitBadArguments = 1;
	private const int ExitParentStillRunning = 2;
	private const int ExitInvalidArchive = 3;
	private const int ExitApplyFailed = 4;
	private const int ExitRestartFailed = 5;

	private static readonly TimeSpan ParentExitTimeout = TimeSpan.FromSeconds(60);

	private static int Main(string[] args)
	{
		if (!UpdaterArguments.TryParse(args, out UpdaterArguments? arguments, out string? error))
		{
			Console.Error.WriteLine(error);
			return ExitBadArguments;
		}

		UpdaterLog log = UpdaterLog.Create(arguments!.LogPath);
		log.Write($"Старт обновления: {arguments.ZipPath} -> {arguments.TargetDirectory}");

		if (!WaitForParentExit(arguments.ParentProcessId))
		{
			log.Write($"Процесс {arguments.ParentProcessId} не завершился за {ParentExitTimeout.TotalSeconds} с — обновление отменено");
			return ExitParentStillRunning;
		}

		int exitCode = ApplyUpdate(arguments, log);

		// Возвращаем пользователя в приложение в любом случае: после отката там рабочая старая версия.
		if (!RestartApplication(arguments, log))
			return ExitRestartFailed;

		return exitCode;
	}

	private static int ApplyUpdate(UpdaterArguments arguments, UpdaterLog log)
	{
		string stagingDirectory = Path.Combine(Path.GetTempPath(), "YamBassPlayer", $"staging-{Guid.NewGuid():N}");
		string backupDirectory = Path.Combine(Path.GetTempPath(), "YamBassPlayer", $"backup-{DateTime.Now:yyyyMMdd-HHmmss}");

		try
		{
			UpdateArchive.Validate(arguments.ZipPath);

			IReadOnlyList<string> payload = UpdateArchive.ExtractPayload(arguments.ZipPath, stagingDirectory);
			UpdateApplyResult applied = UpdateApplier.Apply(payload, arguments.TargetDirectory, backupDirectory);

			log.Write($"Обновление применено: заменено {applied.Replaced}, добавлено {applied.Added}. Бэкап: {backupDirectory}");

			// Архив больше не нужен: апдейтер работает из temp, а не из него.
			TryDeleteFile(arguments.ZipPath);
			return ExitSuccess;
		}
		catch (InvalidDataException exception)
		{
			log.Write($"Архив не принят: {exception.Message}");
			return ExitInvalidArchive;
		}
		catch (Exception exception)
		{
			log.Write($"Ошибка применения обновления: {exception}");
			return ExitApplyFailed;
		}
		finally
		{
			TryDeleteDirectory(stagingDirectory);
		}
	}

	private static bool WaitForParentExit(int parentProcessId)
	{
		try
		{
			using Process parent = Process.GetProcessById(parentProcessId);
			return parent.WaitForExit((int)ParentExitTimeout.TotalMilliseconds);
		}
		catch (ArgumentException)
		{
			return true; // процесс уже завершился
		}
		catch (InvalidOperationException)
		{
			return true;
		}
	}

	private static bool RestartApplication(UpdaterArguments arguments, UpdaterLog log)
	{
		string path = Path.Combine(arguments.TargetDirectory, arguments.RestartFileName);

		try
		{
			Process.Start(new ProcessStartInfo(path)
			{
				UseShellExecute = true,
				WorkingDirectory = arguments.TargetDirectory
			});

			log.Write($"Приложение запущено: {path}");
			return true;
		}
		catch (Exception exception)
		{
			log.Write($"Не удалось запустить приложение: {exception}");
			return false;
		}
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, recursive: true);
		}
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
	}
}
