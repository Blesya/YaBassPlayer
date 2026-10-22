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
	public const string PluginsDirectoryName = "plugins";

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
			|| fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
			// Многофайловая публикация: без этих файлов приложение не запустится.
			|| fileName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
			|| fileName.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Файл плагина: <c>plugins/&lt;id&gt;/...</c>. Плагин — отдельная сборка со своими
	/// зависимостями, поэтому внутри его каталога разрешены подпапки и любые файлы,
	/// но не обход путей и не выход за пределы <c>plugins/</c>.
	/// </summary>
	public static bool IsPluginPayloadPath(string? relativePath)
	{
		if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
			return false;

		string[] segments = relativePath.Split(['/', '\\'], StringSplitOptions.None);

		if (segments.Length < 3)
			return false;

		if (!string.Equals(segments[0], PluginsDirectoryName, StringComparison.OrdinalIgnoreCase))
			return false;

		foreach (string segment in segments)
		{
			if (segment.Length == 0 || segment is "." or "..")
				return false;

			if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
				return false;
		}

		return true;
	}

	/// <summary>Запись архива, которую апдейтер вправе распаковать.</summary>
	public static bool IsAllowedEntry(string? entryPath)
	{
		if (string.IsNullOrEmpty(entryPath))
			return false;

		if (entryPath.Contains('/') || entryPath.Contains('\\'))
			return IsPluginPayloadPath(entryPath);

		return IsPayloadFile(entryPath);
	}

	/// <summary>Проверяет, что архив безопасен (без обхода путей) и содержит приложение.</summary>
	public static void Validate(string zipPath)
	{
		using ZipArchive archive = ZipFile.OpenRead(zipPath);
		bool hasApplication = false;

		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			if (string.IsNullOrEmpty(entry.Name))
				continue; // запись каталога

			string name = entry.FullName;

			if (Path.IsPathRooted(name) || name.Contains(".."))
				throw new InvalidDataException($"Архив содержит недопустимый путь: {name}");

			if (name.Contains('/') || name.Contains('\\'))
			{
				if (!IsPluginPayloadPath(name))
					throw new InvalidDataException($"Архив содержит недопустимый путь: {name}");
			}

			if (string.Equals(name, AppExeName, StringComparison.OrdinalIgnoreCase))
				hasApplication = true;
		}

		if (!hasApplication)
			throw new InvalidDataException($"В архиве нет {AppExeName}");
	}

	/// <summary>
	/// Распаковывает файлы по белому списку, сохраняя раскладку плагинов,
	/// и возвращает их пути в каталоге <paramref name="stagingDirectory"/>.
	/// </summary>
	public static IReadOnlyList<string> ExtractPayload(string zipPath, string stagingDirectory)
	{
		Directory.CreateDirectory(stagingDirectory);
		string stagingRoot = Path.GetFullPath(stagingDirectory);
		List<string> extracted = [];

		using ZipArchive archive = ZipFile.OpenRead(zipPath);

		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			if (!IsAllowedEntry(entry.FullName))
				continue;

			string relativePath = entry.FullName.Replace('\\', '/');
			string destination = Path.GetFullPath(Path.Combine(stagingRoot, relativePath));

			if (!destination.StartsWith(stagingRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"Архив содержит недопустимый путь: {entry.FullName}");

			Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
			entry.ExtractToFile(destination, overwrite: true);
			extracted.Add(destination);
		}

		return extracted;
	}
}
