using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Serilog;
using Terminal.Gui;
using YamBassPlayer.Configuration;

namespace YamBassPlayer.TerminalOutput;

/// <summary>
/// Создаёт VT/ANSI-драйвер Terminal.Gui с поддержкой TrueColor. Штатный драйвер
/// Terminal.Gui на Windows (Win32 CHAR_INFO) умеет только 16 цветов, поэтому для
/// 24-битных обложек нужен <see cref="NetDriver"/> из <c>Drivers/Net</c>.
/// Если терминал не умеет VT или драйвер не поднялся, возвращаем false —
/// приложение стартует на штатном драйвере, как раньше.
/// </summary>
public static class TrueColorDriverFactory
{
	private const int StdOutputHandle = -11;
	private const uint EnableVirtualTerminalProcessing = 0x0004;

	public static bool TryCreate(out ConsoleDriver? driver, out IMainLoopDriver? mainLoopDriver)
	{
		driver = null;
		mainLoopDriver = null;

		if (!OperatingSystem.IsWindows())
			return false;

		string mode = AppConfiguration.GetTrueColorMode();
		if (string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
		{
			Log.Information("TrueColor отключён в конфиге");
			return false;
		}

		if (!TryEnableVirtualTerminalProcessing())
		{
			Log.Information("VT-режим недоступен, остаёмся на штатном драйвере");
			return false;
		}

		try
		{
			// NetDriver пишет через Console.Write, поэтому нужен UTF-8:
			// иначе глифы зависят от кодовой страницы conhost.
			Console.OutputEncoding = new UTF8Encoding(false);

			var netDriver = new NetDriver();
			driver = netDriver;
			mainLoopDriver = new NetMainLoop(netDriver);
			TerminalColorMode.TrueColor = true;
			Log.Information("Включён TrueColor-драйвер (VT/ANSI)");
			return true;
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Не удалось включить TrueColor-драйвер, остаёмся на штатном");
			TerminalColorMode.TrueColor = false;
			driver = null;
			mainLoopDriver = null;
			return false;
		}
	}

	[SupportedOSPlatform("windows")]
	private static bool TryEnableVirtualTerminalProcessing()
	{
		IntPtr handle = GetStdHandle(StdOutputHandle);
		if (handle == IntPtr.Zero || handle == new IntPtr(-1))
			return false;

		if (!GetConsoleMode(handle, out uint mode))
			return false;

		if ((mode & EnableVirtualTerminalProcessing) == 0)
			SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);

		return true;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr GetStdHandle(int nStdHandle);

	[DllImport("kernel32.dll")]
	private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

	[DllImport("kernel32.dll")]
	private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
