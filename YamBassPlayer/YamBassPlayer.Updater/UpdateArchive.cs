using System.IO.Compression;

namespace YamBassPlayer.Updater;

/// <summary>
/// Разбор архива обновления: проверка раскладки и извлечение только тех файлов,
/// которые апдейтер имеет право положить в каталог установки.
/// </summary>
public static class UpdateArchive
{
	public const string AppExeName = "YamBassPlayer.exe";
	public const string UpdaterExeName = "YamBassPlayer.Updater.exe";
	public const string ExampleConfigName = "appsettings.example.json";

	/// <summary>
	/// Файл из корня архива, который разрешено записать в каталог установки.
	/// Данные пользователя (appsettings.json, tracks_cache.db, tracks/, covers/, logs/)
	/// в этот список не входят никогда.
	/// </summary>
	public static bool IsPayloadFile(string? fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			return false;

		return string.Equals(fileName, AppExeName, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, UpdaterExeName, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(fileName, ExampleConfigName, StringComparison.OrdinalIgnoreCase)
			|| fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Проверяет, что архив безопасен (плоская раскладка, без обхода путей) и содержит приложение.</summary>
	public static void Validate(string zipPath)
	{
		using ZipArchive archive = ZipFile.OpenRead(zipPath);
		bool hasApplication = false;

		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			if (string.IsNullOrEmpty(entry.Name))
				continue; // запись каталога

			string name = entry.FullName;

			if (name.Contains('/') || name.Contains('\\') || name.Contains("..") || Path.IsPathRooted(name))
				throw new InvalidDataException($"Архив содержит недопустимый путь: {name}");

			if (string.Equals(name, AppExeName, StringComparison.OrdinalIgnoreCase))
				hasApplication = true;
		}

		if (!hasApplication)
			throw new InvalidDataException($"В архиве нет {AppExeName}");
	}

	/// <summary>Распаковывает файлы по белому списку и возвращает их пути.</summary>
	public static IReadOnlyList<string> ExtractPayload(string zipPath, string stagingDirectory)
	{
		Directory.CreateDirectory(stagingDirectory);
		List<string> extracted = [];

		using ZipArchive archive = ZipFile.OpenRead(zipPath);

		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			if (!IsPayloadFile(entry.Name))
				continue;

			string destination = Path.Combine(stagingDirectory, entry.Name);
			entry.ExtractToFile(destination, overwrite: true);
			extracted.Add(destination);
		}

		return extracted;
	}
}
