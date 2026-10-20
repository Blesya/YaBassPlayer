namespace YamBassPlayer.Updater;

/// <summary>
/// Простой файловый лог. Апдейтер запускается без окна консоли, поэтому разбираться
/// с неудачным обновлением приходится по этому файлу.
/// </summary>
public sealed class UpdaterLog
{
	private readonly object _syncRoot = new();
	private readonly string? _path;

	private UpdaterLog(string? path) => _path = path;

	public static UpdaterLog Create(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return new UpdaterLog(null);

		try
		{
			string? directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);
		}
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }

		return new UpdaterLog(path);
	}

	public void Write(string message)
	{
		string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";

		lock (_syncRoot)
		{
			try
			{
				Console.WriteLine(line);
			}
			catch (IOException) { }

			if (_path is null)
				return;

			try
			{
				File.AppendAllText(_path, line + Environment.NewLine);
			}
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}
}
