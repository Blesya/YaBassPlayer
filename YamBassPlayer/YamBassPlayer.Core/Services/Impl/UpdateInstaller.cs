using System.Diagnostics;
using System.Globalization;
using Serilog;
using YamBassPlayer.Constants;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// тХи╨зтХитЦСтХитФРтХд╨УтХд╨СтХитХСтХитЦСтХитХбтХд╨Т <c>YamBassPlayer.Updater.exe</c> тХитХХтХитХЦ тХитЦУтХд╨РтХитХбтХитХЭтХитХбтХитХЬтХитХЬтХитХЫтХитФВтХитХЫ тХитХСтХитЦСтХд╨ТтХитЦСтХитХЧтХитХЫтХитФВтХитЦС тХд╨С тХд╨УтХитХвтХитХб тХд╨СтХитХСтХитЦСтХд╨ЧтХитЦСтХитХЬтХитХЬтХд╨ЫтХитХЭ тХитЦСтХд╨РтХд╨ХтХитХХтХитЦУтХитХЫтХитХЭ.
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
				Log.Error("тХи╨нтХитХб тХитХЬтХитЦСтХитХгтХитФдтХитХбтХитХЬ {Updater} тХд╨РтХд╨ЯтХитФдтХитХЫтХитХЭ тХд╨С тХитФРтХд╨РтХитХХтХитХЧтХитХЫтХитХвтХитХбтХитХЬтХитХХтХитХбтХитХЭ ╤В╨Р╨д тХитХЫтХитЦТтХитХЬтХитХЫтХитЦУтХитХЧтХитХбтХитХЬтХитХХтХитХб тХитХЬтХитХб тХитЦТтХд╨УтХитФдтХитХбтХд╨Т тХд╨УтХд╨СтХд╨ТтХитЦСтХитХЬтХитХЫтХитЦУтХитХЧтХитХбтХитХЬтХитХЫ", AppInfo.UpdaterExeName);
				return;
			}

			// тХи╨атХитФРтХитФдтХитХбтХитХгтХд╨ТтХитХбтХд╨Р тХитХЫтХитЦТтХд╨ЯтХитХЦтХитЦСтХитХЬ тХд╨РтХитЦСтХитЦТтХитХЫтХд╨ТтХитЦСтХд╨ТтХд╨Ь тХитХХтХитХЦ temp: тХитХХтХитХЬтХитЦСтХд╨ЧтХитХб тХитХЫтХитХЬ тХитХЬтХитХб тХд╨СтХитХЭтХитХЫтХитХвтХитХбтХд╨Т тХитХЦтХитЦСтХитХЭтХитХбтХитХЬтХитХХтХд╨ТтХд╨Ь тХд╨СтХитХЫтХитЦТтХд╨СтХд╨ТтХитЦУтХитХбтХитХЬтХитХЬтХд╨ЫтХитХг exe.
			// тХи╨ктХитХЫтХитФРтХитХХтХд╨РтХд╨УтХитХбтХитХЭ тХитЦУтХитХбтХд╨СтХд╨Ь тХитХЬтХитЦСтХитЦТтХитХЫтХд╨Р тХд╨ФтХитЦСтХитХгтХитХЧтХитХЫтХитЦУ, тХитЦС тХитХЬтХитХб тХд╨ТтХитХЫтХитХЧтХд╨ЬтХитХСтХитХЫ exe: тХитФРтХд╨УтХитЦТтХитХЧтХитХХтХитХСтХитЦСтХд╨ЦтХитХХтХд╨Я тХитФРтХитЦСтХитФРтХитХСтХитХЫтХитХг тХд╨ТтХд╨РтХитХбтХитЦТтХд╨УтХитХбтХд╨Т
			// тХд╨РтХд╨ЯтХитФдтХитХЫтХитХЭ .dll, .deps.json тХитХХ .runtimeconfig.json.
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
			Log.Information("тХи╨зтХитЦСтХитФРтХд╨УтХд╨ЩтХитХбтХитХЬ тХитЦСтХитФРтХитФдтХитХбтХитХгтХд╨ТтХитХбтХд╨Р тХитФдтХитХЧтХд╨Я тХитЦУтХитХбтХд╨РтХд╨СтХитХХтХитХХ {Version}", _pendingVersion);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "тХи╨нтХитХб тХд╨УтХитФдтХитЦСтХитХЧтХитХЫтХд╨СтХд╨Ь тХитХЦтХитЦСтХитФРтХд╨УтХд╨СтХд╨ТтХитХХтХд╨ТтХд╨Ь тХитЦСтХитФРтХитФдтХитХбтХитХгтХд╨ТтХитХбтХд╨Р");
		}
		finally
		{
			_pendingZipPath = null;
			_pendingVersion = null;
		}
	}
}
