using System.Globalization;

namespace YamBassPlayer.Updater;

/// <summary>
/// Аргументы командной строки апдейтера. Приложение запускает его уже после того,
/// как решило обновиться, поэтому здесь только файловая часть: чей процесс ждать,
/// какой архив ставить, куда и что перезапускать.
/// </summary>
public sealed record UpdaterArguments(
	int ParentProcessId,
	string ZipPath,
	string TargetDirectory,
	string RestartFileName,
	string? LogPath)
{
	public const string DefaultRestartFileName = "YamBassPlayer.exe";

	public static bool TryParse(string[] args, out UpdaterArguments? result, out string? error)
	{
		result = null;
		error = null;

		int? parentProcessId = null;
		string? zipPath = null;
		string? targetDirectory = null;
		string? restartFileName = null;
		string? logPath = null;

		for (int i = 0; i < args.Length; i++)
		{
			string key = args[i];

			if (i + 1 >= args.Length)
			{
				error = $"Не задано значение для аргумента {key}";
				return false;
			}

			string value = args[++i];

			switch (key)
			{
				case "--pid":
					if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
					{
						error = $"Некорректный идентификатор процесса: {value}";
						return false;
					}

					parentProcessId = pid;
					break;

				case "--zip":
					zipPath = value;
					break;

				case "--target":
					targetDirectory = value;
					break;

				case "--restart":
					restartFileName = value;
					break;

				case "--log":
					logPath = value;
					break;

				default:
					error = $"Неизвестный аргумент: {key}";
					return false;
			}
		}

		if (parentProcessId is null)
		{
			error = "Не задан --pid";
			return false;
		}

		if (string.IsNullOrWhiteSpace(zipPath))
		{
			error = "Не задан --zip";
			return false;
		}

		if (string.IsNullOrWhiteSpace(targetDirectory))
		{
			error = "Не задан --target";
			return false;
		}

		result = new UpdaterArguments(
			parentProcessId.Value,
			zipPath!,
			targetDirectory!,
			string.IsNullOrWhiteSpace(restartFileName) ? DefaultRestartFileName : restartFileName!,
			string.IsNullOrWhiteSpace(logPath) ? null : logPath);

		return true;
	}
}
