namespace YamBassPlayer.Updater;

/// <summary>Итог применения обновления: сколько файлов заменено и сколько добавлено.</summary>
public sealed record UpdateApplyResult(int Replaced, int Added);

/// <summary>
/// Копирует файлы обновления в каталог установки, сохраняя бэкап заменяемых файлов
/// и откатываясь при любой ошибке. Данные пользователя не трогает; в папке плагина
/// удаляет файлы, которых нет в новой версии, — иначе старый DLL может загрузиться
/// вместо нового.
/// </summary>
public static class UpdateApplier
{
	/// <param name="stagingDirectory">
	/// Корень распакованного обновления. Если задан, раскладка файлов внутри него
	/// сохраняется (нужно для <c>plugins/&lt;id&gt;/...</c>); иначе берётся только имя файла.
	/// </param>
	public static UpdateApplyResult Apply(
		IReadOnlyList<string> payloadFiles,
		string targetDirectory,
		string backupDirectory,
		string? stagingDirectory = null)
	{
		Directory.CreateDirectory(targetDirectory);
		Directory.CreateDirectory(backupDirectory);

		List<(string Destination, string Backup)> replaced = [];
		List<string> added = [];
		List<(string Destination, string Backup)> removed = [];
		var payloadDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		try
		{
			foreach (string source in payloadFiles)
			{
				string relativePath = stagingDirectory is null
					? Path.GetFileName(source)
					: Path.GetRelativePath(stagingDirectory, source);

				string destination = Path.GetFullPath(Path.Combine(targetDirectory, relativePath));
				payloadDestinations.Add(destination);
				Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

				if (File.Exists(destination))
				{
					string backup = Path.Combine(backupDirectory, relativePath);
					Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
					File.Copy(destination, backup, overwrite: true);
					replaced.Add((destination, backup));
				}
				else
				{
					added.Add(destination);
				}

				File.Copy(source, destination, overwrite: true);
			}

			RemoveObsoletePluginFiles(payloadDestinations, targetDirectory, backupDirectory, removed);
		}
		catch
		{
			Rollback(replaced, added);
			RestoreRemoved(removed);
			throw;
		}

		return new UpdateApplyResult(replaced.Count, added.Count);
	}

	/// <summary>
	/// Для каждой папки <c>plugins/&lt;id&gt;</c> из обновления убирает файлы,
	/// которых в обновлении нет. Удалённые файлы остаются в бэкапе.
	/// </summary>
	private static void RemoveObsoletePluginFiles(
		HashSet<string> payloadDestinations,
		string targetDirectory,
		string backupDirectory,
		List<(string Destination, string Backup)> removed)
	{
		IEnumerable<string> pluginIds = payloadDestinations
			.Select(path => GetPluginId(targetDirectory, path))
			.OfType<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase);

		foreach (string pluginId in pluginIds)
		{
			string pluginDirectory = Path.Combine(targetDirectory, "plugins", pluginId);
			if (!Directory.Exists(pluginDirectory))
				continue;

			foreach (string file in Directory.GetFiles(pluginDirectory, "*", SearchOption.AllDirectories))
			{
				string fullPath = Path.GetFullPath(file);
				if (payloadDestinations.Contains(fullPath))
					continue;

				string relativePath = Path.GetRelativePath(targetDirectory, fullPath);
				string backup = Path.Combine(backupDirectory, relativePath);
				Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
				File.Copy(fullPath, backup, overwrite: true);
				File.Delete(fullPath);
				removed.Add((fullPath, backup));
			}
		}
	}

	private static string? GetPluginId(string targetDirectory, string fullPath)
	{
		string relativePath = Path.GetRelativePath(targetDirectory, fullPath);
		string[] parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

		return parts.Length >= 3 && string.Equals(parts[0], "plugins", StringComparison.OrdinalIgnoreCase)
			? parts[1]
			: null;
	}

	private static void Rollback(List<(string Destination, string Backup)> replaced, List<string> added)
	{
		foreach (var (destination, backup) in replaced)
		{
			try
			{
				File.Copy(backup, destination, overwrite: true);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}

		foreach (string path in added)
		{
			try
			{
				File.Delete(path);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}

	private static void RestoreRemoved(List<(string Destination, string Backup)> removed)
	{
		foreach (var (destination, backup) in removed)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
				File.Copy(backup, destination, overwrite: true);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}
}
