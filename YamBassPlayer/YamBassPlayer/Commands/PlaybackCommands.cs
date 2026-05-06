using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Commands;

/// <summary>play|p — запустить/возобновить воспроизведение, либо play N — играть N-й трек очереди.</summary>
public sealed class PlayCommand(IEventBus eventBus, ITrackRepository trackRepository) : ICommand
{
	public string Name => "play";
	public string Description => "play|p — воспроизвести (без аргументов — возобновить). play|p N — играть N-й трек.";
	public IReadOnlyList<string> Aliases => ["p"];

	public CommandResult Execute(string[] args)
	{
		if (args.Length == 0)
		{
			eventBus.Publish(new ResumeCommandEvent());
			return CommandResult.Ok("Воспроизведение");
		}

		if (!int.TryParse(args[0], System.Globalization.NumberStyles.Integer,
			System.Globalization.CultureInfo.InvariantCulture, out int index) || index < 1)
		{
			return CommandResult.Error("Некорректный номер трека. Ожидается число ≥ 1");
		}

		var ids = trackRepository.GetAllTrackIds();
		if (index > ids.Count)
			return CommandResult.Error($"Номер {index} вне диапазона (всего треков: {ids.Count})");

		eventBus.Publish(new PlayTrackAtCommandEvent(index - 1));
		return CommandResult.Ok($"Воспроизведение трека {index}");
	}
}

/// <summary>pause|ps — пауза.</summary>
public sealed class PauseCommand(IEventBus eventBus) : ICommand
{
	public string Name => "pause";
	public string Description => "pause|ps — поставить на паузу.";
	public IReadOnlyList<string> Aliases => ["ps"];

	public CommandResult Execute(string[] args)
	{
		eventBus.Publish(new PauseCommandEvent());
		return CommandResult.Ok("Пауза");
	}
}

/// <summary>toggle|t — переключить пауза/воспроизведение.</summary>
public sealed class ToggleCommand(IEventBus eventBus) : ICommand
{
	public string Name => "toggle";
	public string Description => "toggle|t — пауза/воспроизведение.";
	public IReadOnlyList<string> Aliases => ["t"];

	public CommandResult Execute(string[] args)
	{
		eventBus.Publish(new PlayPauseCommandEvent());
		return CommandResult.Ok("Переключение");
	}
}

/// <summary>stop|s — остановить.</summary>
public sealed class StopCommand(IEventBus eventBus) : ICommand
{
	public string Name => "stop";
	public string Description => "stop|s — остановить воспроизведение.";
	public IReadOnlyList<string> Aliases => ["s"];

	public CommandResult Execute(string[] args)
	{
		eventBus.Publish(new StopCommandEvent());
		return CommandResult.Ok("Стоп");
	}
}

/// <summary>next|n — следующий трек.</summary>
public sealed class NextCommand(IEventBus eventBus) : ICommand
{
	public string Name => "next";
	public string Description => "next|n — следующий трек.";
	public IReadOnlyList<string> Aliases => ["n"];

	public CommandResult Execute(string[] args)
	{
		eventBus.Publish(new NextCommandEvent());
		return CommandResult.Ok("Следующий трек");
	}
}

/// <summary>prev|b — предыдущий трек.</summary>
public sealed class PreviousCommand(IEventBus eventBus) : ICommand
{
	public string Name => "prev";
	public string Description => "prev|b — предыдущий трек.";
	public IReadOnlyList<string> Aliases => ["b"];

	public CommandResult Execute(string[] args)
	{
		eventBus.Publish(new PreviousCommandEvent());
		return CommandResult.Ok("Предыдущий трек");
	}
}

/// <summary>restart|r — перезапустить текущий трек.</summary>
public sealed class RestartCommand(IEventBus eventBus) : ICommand
{
	public string Name => "restart";
	public string Description => "restart|r — перезапустить текущий трек.";
	public IReadOnlyList<string> Aliases => ["r"];

	public CommandResult Execute(string[] args)
	{
		eventBus.Publish(new RestartCommandEvent());
		return CommandResult.Ok("Перезапуск трека");
	}
}

/// <summary>seek — перемотка в процентах (0-100).</summary>
public sealed class SeekCommand(IEventBus eventBus) : ICommand
{
	public string Name => "seek";
	public string Description => "seek <0-100> — перемотать на процент.";
	public IReadOnlyList<string> Aliases => [];

	public CommandResult Execute(string[] args)
	{
		if (args.Length == 0)
			return CommandResult.Error("Укажите процент: seek 50");

		if (!int.TryParse(args[0], System.Globalization.NumberStyles.Integer,
			System.Globalization.CultureInfo.InvariantCulture, out int percent)
			|| percent < 0 || percent > 100)
		{
			return CommandResult.Error("Некорректный процент. Ожидается число от 0 до 100");
		}

		eventBus.Publish(new SeekCommandEvent(percent));
		return CommandResult.Ok($"Перемотка на {percent}%");
	}
}

/// <summary>mode — режим воспроизведения: seq/последовательно или shuffle/случайно.</summary>
public sealed class ModeCommand(IEventBus eventBus) : ICommand
{
	public string Name => "mode";
	public string Description => "mode <seq|shuffle> — режим воспроизведения.";
	public IReadOnlyList<string> Aliases => [];

	public CommandResult Execute(string[] args)
	{
		if (args.Length == 0)
			return CommandResult.Error("Укажите режим: mode seq или mode shuffle");

		string mode = args[0].ToLowerInvariant() switch
		{
			"seq" or "sequential" or "последовательно" or "по-очереди" or "поочередно" => "seq",
			"shuffle" or "random" or "случайно" or "перемешать" => "shuffle",
			_ => string.Empty
		};

		if (mode.Length == 0)
			return CommandResult.Error("Некорректный режим. Ожидается seq или shuffle");

		bool shuffle = mode == "shuffle";
		eventBus.Publish(new ShuffleCommandEvent(shuffle));
		return CommandResult.Ok(shuffle ? "Режим: случайно" : "Режим: поочерёдно");
	}
}
