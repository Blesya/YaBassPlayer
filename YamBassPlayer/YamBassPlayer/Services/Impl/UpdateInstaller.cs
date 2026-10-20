using System.Diagnostics;
using System.Globalization;
using Serilog;
using YamBassPlayer.Constants;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Запускает <c>YamBassPlayer.Updater.exe</c> из временного каталога с уже скачанным архивом.
/// </summary>
public sealed class UpdateInstaller : IUpdateInstaller
{
	private string? _pendingVersion;
	private string? _pendingZipPath;

	public void Schedule(string zipPath, string version)
	{
		_pendingZipPath = zipPath;
		_pendingVersion = version;
	}

	public void LaunchPending()
	{
		if (_pendingZipPath is null)
			return;

		try
		{
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string workDirectory = Path.GetDirectoryName(_pendingZipPath)!;
			string updaterSource = Path.Combine(baseDirectory, AppInfo.UpdaterExeName);
			string updaterPath = Path.Combine(workDirectory, AppInfo.UpdaterExeName);

			if (!File.Exists(updaterSource))
			{
				Log.Error("Не найден {Updater} рядом с приложением — обновление не будет установлено", AppInfo.UpdaterExeName);
				return;
			}

			// Апдейтер обязан работать из temp: иначе он не сможет заменить собственный exe в каталоге установки.
			File.Copy(updaterSource, updaterPath, overwrite: true);

			ProcessStartInfo startInfo = new(updaterPath)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = workDirectory
			};

			startInfo.ArgumentList.Add("--pid");
			startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
			startInfo.ArgumentList.Add("--zip");
			startInfo.ArgumentList.Add(_pendingZipPath);
			startInfo.ArgumentList.Add("--target");
			startInfo.ArgumentList.Add(baseDirectory.TrimEnd(Path.DirectorySeparatorChar));
			startInfo.ArgumentList.Add("--restart");
			startInfo.ArgumentList.Add(AppInfo.AppExeName);
			startInfo.ArgumentList.Add("--log");
			startInfo.ArgumentList.Add(Path.Combine(workDirectory, "updater.log"));

			Process.Start(startInfo);
			Log.Information("Запущен апдейтер для версии {Version}", _pendingVersion);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Не удалось запустить апдейтер");
		}
		finally
		{
			_pendingZipPath = null;
			_pendingVersion = null;
		}
	}
}
