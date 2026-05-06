namespace YamBassPlayer.Commands;

/// <summary>
/// Результат выполнения команды. <see cref="Success"/> определяет, «ок» это или «не ок» —
/// presenter показывает сообщение в строке результата.
/// </summary>
public sealed record CommandResult(bool Success, string Message)
{
	public static CommandResult Ok(string message = "ОК") => new(true, message);
	public static CommandResult Error(string message) => new(false, message);
}

/// <summary>
/// Контракт отдельной команды. Новую команду достаточно добавить как класс,
/// реализующий <see cref="ICommand"/>, и зарегистрировать в DI — парсер и UI не трогаются.
/// </summary>
public interface ICommand
{
	/// <summary>Каноническое имя команды (используется в help).</summary>
	string Name { get; }
	/// <summary>Описание для help.</summary>
	string Description { get; }
	/// <summary>Короткие алиасы (например "p", "ps").</summary>
	IReadOnlyList<string> Aliases { get; }

	/// <summary>Выполняет команду. <paramref name="args"/> — аргументы после имени/алиаса.</summary>
	CommandResult Execute(string[] args);
}

public static class CommandHelpers
{
	public static bool TryParseInt(string text, out int value)
		=> int.TryParse(text, System.Globalization.NumberStyles.Integer,
			System.Globalization.CultureInfo.InvariantCulture, out value);
}
