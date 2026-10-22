using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Размер и положение терминального окна. Размер меняется запросом DTTERM/XTWINOPS
/// (<c>CSI 8 ; rows ; cols t</c>): его понимают и conhost, и Windows Terminal 1.23+.
/// Win32 <c>Console.SetWindowSize</c> для этого не годится — Windows Terminal
/// рапортует об успехе, но само окно не меняет. Положение задаётся Win32
/// <c>SetWindowPos</c> для окна терминала (в WT — его верхнеуровневого окна).
/// </summary>
public sealed class ConsoleWindow : IConsoleWindow
{
	/// <summary>Ниже этого размера интерфейс плеера не помещается, поэтому не восстанавливаем.</summary>
	private const int MinWidth = 80;
	private const int MinHeight = 24;

	/// <summary>Верхняя граница защищает от битого значения в конфиге.</summary>
	private const int MaxWidth = 1000;
	private const int MaxHeight = 300;

	/// <summary>Сколько ждём, пока терминал применит размер и сообщит его обратно.</summary>
	private const int ApplyTimeoutMs = 300;
	private const int ApplyPollMs = 10;

	private const int StdOutputHandle = -11;
	private const uint EnableVirtualTerminalProcessing = 0x0004;

	private const uint GaRootOwner = 3;
	private const uint MonitorDefaultToNull = 0;
	private const uint SwpNoSize = 0x0001;
	private const uint SwpNoZOrder = 0x0004;
	private const uint SwpNoActivate = 0x0010;

	public ConsoleWindowSize? GetSize()
	{
		try
		{
			int width = Console.WindowWidth;
			int height = Console.WindowHeight;
			return width > 0 && height > 0 ? new ConsoleWindowSize(width, height) : null;
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось прочитать размер терминального окна");
			return null;
		}
	}

	public bool TrySetSize(ConsoleWindowSize size)
	{
		int width = Math.Clamp(size.Width, MinWidth, MaxWidth);
		int height = Math.Clamp(size.Height, MinHeight, MaxHeight);

		try
		{
			if (Console.WindowWidth == width && Console.WindowHeight == height)
				return true;

			// Без консоли (вывод перенаправлен) последовательность писать некуда.
			if (OperatingSystem.IsWindows())
			{
				if (!TryEnableVirtualTerminalProcessing())
					return false;
			}
			else if (Console.IsOutputRedirected)
			{
				return false;
			}

			Console.Out.Write($"\u001b[8;{height};{width}t");
			Console.Out.Flush();

			// Ждём, пока терминал применит размер: иначе первый layout прочитает старый.
			// Если терминал запрос игнорирует (несколько вкладок/панелей), вернём false.
			for (int waited = 0; waited < ApplyTimeoutMs; waited += ApplyPollMs)
			{
				if (Console.WindowWidth == width && Console.WindowHeight == height)
					return true;
				Thread.Sleep(ApplyPollMs);
			}

			return false;
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось выставить размер терминального окна {Width}x{Height}", width, height);
			return false;
		}
	}

	/// <summary>
	/// Включает обработку VT-последовательностей в выводе консоли. В ConPTY
	/// (Windows Terminal) она обычно уже включена, в классическом conhost — нет.
	/// </summary>
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

	public ConsoleWindowPosition? GetPosition()
	{
		if (!OperatingSystem.IsWindows())
			return null;

		try
		{
			IntPtr window = GetTerminalWindow();
			if (window == IntPtr.Zero || !GetWindowRect(window, out NativeRect rect))
				return null;

			return new ConsoleWindowPosition(rect.Left, rect.Top);
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось прочитать положение терминального окна");
			return null;
		}
	}

	public bool TrySetPosition(ConsoleWindowPosition position)
	{
		if (!OperatingSystem.IsWindows())
			return false;

		try
		{
			IntPtr window = GetTerminalWindow();
			if (window == IntPtr.Zero)
				return false;

			// Сохранённый монитор мог исчезнуть: не ставим окно в пустоту.
			if (MonitorFromPoint(new NativePoint { X = position.X, Y = position.Y }, MonitorDefaultToNull) == IntPtr.Zero)
				return false;

			return SetWindowPos(window, IntPtr.Zero, position.X, position.Y, 0, 0,
				SwpNoSize | SwpNoZOrder | SwpNoActivate);
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось восстановить положение терминального окна {X},{Y}", position.X, position.Y);
			return false;
		}
	}

	/// <summary>
	/// В conhost это само окно консоли, в Windows Terminal — его верхнеуровневое окно
	/// (окно псевдоконсоли является дочерним/owned-окном WT).
	/// </summary>
	[SupportedOSPlatform("windows")]
	private static IntPtr GetTerminalWindow()
	{
		IntPtr console = GetConsoleWindow();
		if (console == IntPtr.Zero)
			return IntPtr.Zero;

		IntPtr root = GetAncestor(console, GaRootOwner);
		return root != IntPtr.Zero ? root : console;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr GetStdHandle(int nStdHandle);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;
		public int Y;
	}
}
