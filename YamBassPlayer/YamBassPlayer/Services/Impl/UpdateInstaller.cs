using System.Diagnostics;
using System.Globalization;
using Serilog;
using YamBassPlayer.Constants;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// ╨Ч╨░╨┐╤Г╤Б╨║╨░╨╡╤В <c>YamBassPlayer.Updater.exe</c> ╨╕╨╖ ╨▓╤А╨╡╨╝╨╡╨╜╨╜╨╛╨│╨╛ ╨║╨░╤В╨░╨╗╨╛╨│╨░ ╤Б ╤Г╨╢╨╡ ╤Б╨║╨░╤З╨░╨╜╨╜╤Л╨╝ ╨░╤А╤Е╨╕╨▓╨╛╨╝.
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
				Log.Error("╨Э╨╡ ╨╜╨░╨╣╨┤╨╡╨╜ {Updater} ╤А╤П╨┤╨╛╨╝ ╤Б ╨┐╤А╨╕╨╗╨╛╨╢╨╡╨╜╨╕╨╡╨╝ тАФ ╨╛╨▒╨╜╨╛╨▓╨╗╨╡╨╜╨╕╨╡ ╨╜╨╡ ╨▒╤Г╨┤╨╡╤В ╤Г╤Б╤В╨░╨╜╨╛╨▓╨╗╨╡╨╜╨╛", AppInfo.UpdaterExeName);
				return;
			}

			// ╨Р╨┐╨┤╨╡╨╣╤В╨╡╤А ╨╛╨▒╤П╨╖╨░╨╜ ╤А╨░╨▒╨╛╤В╨░╤В╤М ╨╕╨╖ temp: ╨╕╨╜╨░╤З╨╡ ╨╛╨╜ ╨╜╨╡ ╤Б╨╝╨╛╨╢╨╡╤В ╨╖╨░╨╝╨╡╨╜╨╕╤В╤М ╤Б╨╛╨▒╤Б╤В╨▓╨╡╨╜╨╜╤Л╨╣ exe.
			// ╨Ъ╨╛╨┐╨╕╤А╤Г╨╡╨╝ ╨▓╨╡╤Б╤М ╨╜╨░╨▒╨╛╤А ╤Д╨░╨╣╨╗╨╛╨▓, ╨░ ╨╜╨╡ ╤В╨╛╨╗╤М╨║╨╛ exe: ╨┐╤Г╨▒╨╗╨╕╨║╨░╤Ж╨╕╤П ╨┐╨░╨┐╨║╨╛╨╣ ╤В╤А╨╡╨▒╤Г╨╡╤В
			// ╤А╤П╨┤╨╛╨╝ .dll, .deps.json ╨╕ .runtimeconfig.json.
			string updaterPrefix = Path.GetFileNameWithoutExtension(AppInfo.UpdaterExeName);
			foreach (string file in Directory.GetFiles(baseDirectory, updaterPrefix + ".*"))
				File.Copy(file, Path.Combine(workDirectory, Path.GetFileName(file)), overwrite: true);

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
			Log.Information("╨Ч╨░╨┐╤Г╤Й╨╡╨╜ ╨░╨┐╨┤╨╡╨╣╤В╨╡╤А ╨┤╨╗╤П ╨▓╨╡╤А╤Б╨╕╨╕ {Version}", _pendingVersion);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "╨Э╨╡ ╤Г╨┤╨░╨╗╨╛╤Б╤М ╨╖╨░╨┐╤Г╤Б╤В╨╕╤В╤М ╨░╨┐╨┤╨╡╨╣╤В╨╡╤А");
		}
		finally
		{
			_pendingZipPath = null;
			_pendingVersion = null;
		}
	}
}
