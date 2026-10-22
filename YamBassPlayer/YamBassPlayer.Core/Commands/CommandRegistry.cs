using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Commands;

/// <summary>
/// Разбирает сырой ввод (verb + args) и резолвит команду по имени или алиасу.
/// Коллекция команд подтягивается из DI как IEnumerable&lt;ICommand&gt;.
/// </summary>
public sealed class CommandRegistry
{
	private readonly Dictionary<string, ICommand> _commands;
	private readonly List<ICommand> _commandList = [];
	private readonly IEventBus? _eventBus;

	public IReadOnlyList<ICommand> Commands => _commandList;

	public CommandRegistry(IEnumerable<ICommand> commands, IEventBus? eventBus = null)
	{
		_eventBus = eventBus;
		_commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
		foreach (ICommand command in commands)
			Add(command);
	}

	/// <summary>Добавляет команду плагина после сборки контейнера; первый по имени побеждает.</summary>
	public void Add(ICommand command)
	{
		ArgumentNullException.ThrowIfNull(command);

		if (!_commands.TryAdd(command.Name, command))
			return;

		_commandList.Add(command);
		foreach (string alias in command.Aliases)
			_commands.TryAdd(alias, command);
	}

	public CommandResult Execute(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return CommandResult.Error("Пустая команда. Введите help");

		Logging.LogUserAction($"команда «{raw}»");

		var parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		string verb = parts[0];
		string[] args = parts.Skip(1).ToArray();

		if (string.Equals(verb, "help", StringComparison.OrdinalIgnoreCase) || verb == "?")
		{
			string helpText = string.Join(Environment.NewLine, Commands
				.Select(c => c.Description)
				.Distinct());

			if (_eventBus is not null)
			{
				_eventBus.Publish(new HelpCommandEvent(helpText.Length == 0 ? "Нет доступных команд" : helpText));
				return CommandResult.Ok("Справка открыта");
			}

			return CommandResult.Ok(helpText.Length == 0 ? "Нет доступных команд" : helpText);
		}

		if (!_commands.TryGetValue(verb, out var command))
			return CommandResult.Error($"Неизвестная команда: {verb}. Введите help");

		return command.Execute(args);
	}
}
