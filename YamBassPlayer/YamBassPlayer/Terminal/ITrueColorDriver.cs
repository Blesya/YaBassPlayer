namespace YamBassPlayer.TerminalOutput;

/// <summary>
/// Драйвер, умеющий выводить 24-битный цвет. Обычные представления Terminal.Gui
/// по-прежнему пользуются 16-цветными <see cref="Terminal.Gui.Attribute"/>,
/// а TrueColor-клетки (обложки) задаются отдельно.
/// </summary>
public interface ITrueColorDriver
{
	/// <summary>Цвет для последующих <c>AddRune</c>.</summary>
	void SetRgbAttribute(byte foregroundR, byte foregroundG, byte foregroundB,
		byte backgroundR, byte backgroundG, byte backgroundB);

	/// <summary>Возвращает обычный 16-цветный режим для последующих <c>AddRune</c>.</summary>
	void ResetRgbAttribute();
}
