namespace YamBassPlayer.Updater;

/// <summary>Итог применения обновления: сколько файлов заменено и сколько добавлено.</summary>
public sealed record UpdateApplyResult(int Replaced, int Added);

/// <summary>
/// Копирует файлы обновления в каталог установки, сохраняя бэкап заменяемых файлов
/// и откатываясь при любой ошибке. Файлы, которых нет в списке, не трогает —
/// данные пользователя остаются на месте.
/// </summary>
public static class UpdateApplier
{
	public static UpdateApplyResult Apply(IReadOnlyList<string> payloadFiles, string targetDirectory, string backupDirectory)
	{
		Directory.CreateDirectory(targetDirectory);
		Directory.CreateDirectory(backupDirectory);

		List<(string Destination, string Backup)> replaced = [];
		List<string> added = [];

		try
		{
			foreach (string source in payloadFiles)
			{
				string name = Path.GetFileName(source);
				string destination = Path.Combine(targetDirectory, name);

				if (File.Exists(destination))
				{
					string backup = Path.Combine(backupDirectory, name);
					File.Copy(destination, backup, overwrite: true);
					replaced.Add((destination, backup));
				}
				else
				{
					added.Add(destination);
				}

				File.Copy(source, destination, overwrite: true);
			}
		}
		catch
		{
			Rollback(replaced, added);
			throw;
		}

		return new UpdateApplyResult(replaced.Count, added.Count);
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
}
