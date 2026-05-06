using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Commands;

/// <summary>
/// Разбирает сырой ввод (verb + args) и резолвит команду по имени или алиасу.
/// Коллекция команд подтягивается из DI как IEnumerable&lt;ICommand&gt;.
/// </summary>
public sealed class CommandRegistry
{
	private readonly IReadOnlyDictionary<string, ICommand> _commands;
	private readonly IEventBus? _eventBus;

	public IReadOnlyList<ICommand> Commands { get; }

	public CommandRegistry(IEnumerable<ICommand> commands, IEventBus? eventBus = null)
	{
		Commands = commands.ToList();
		_eventBus = eventBus;
		var lookup = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
		foreach (var command in Commands)
		{
			lookup[command.Name] = command;
			foreach (var alias in command.Aliases)
				lookup[alias] = command;
		}
		_commands = lookup;
	}

	public CommandResult Execute(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return CommandResult.Error("Пустая команда. Введите help");

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
