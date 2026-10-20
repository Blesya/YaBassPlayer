namespace YamBassPlayer.TerminalOutput;

/// <summary>
/// Выбранный при старте режим цвета. Статический по той же причине, что
/// <see cref="AppConfiguration"/> и <see cref="Themes"/>: значение одно на процесс
/// и нужно сервисам, которые создаются до инициализации UI.
/// </summary>
public static class TerminalColorMode
{
	/// <summary>True, если активен драйвер с поддержкой 24-битного цвета.</summary>
	public static bool TrueColor { get; set; }
}
