using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Терминальное окно: чтение текущего размера и best-effort установка сохранённого.
/// </summary>
public interface IConsoleWindow
{
	/// <summary>Текущий размер окна, либо <c>null</c>, если его нельзя прочитать (нет консоли).</summary>
	ConsoleWindowSize? GetSize();

	/// <summary>
	/// Пытается выставить размер окна. Возвращает <c>false</c>, если терминал не позволяет
	/// менять размер (Windows Terminal старше 1.23, несколько вкладок/панелей) или запрос
	/// не применился.
	/// </summary>
	bool TrySetSize(ConsoleWindowSize size);

	/// <summary>Текущее положение окна на экране; <c>null</c>, если платформа его не отдаёт.</summary>
	ConsoleWindowPosition? GetPosition();

	/// <summary>
	/// Передвигает окно. Возвращает <c>false</c>, если платформа/терминал этого не позволяет
	/// или сохранённая точка больше не лежит ни на одном мониторе.
	/// </summary>
	bool TrySetPosition(ConsoleWindowPosition position);
}
