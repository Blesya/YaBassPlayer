using YamBassPlayer.Services;

namespace YamBassPlayer.Commands;

/// <summary>now — показать текущий трек.</summary>
public sealed class NowCommand(IPlaybackQueue playbackQueue) : ICommand
{
	public string Name => "now";
	public string Description => "now — текущий трек.";
	public IReadOnlyList<string> Aliases => ["track"];

	public CommandResult Execute(string[] args)
	{
		var current = playbackQueue.CurrentTrackId;
		if (string.IsNullOrEmpty(current))
			return CommandResult.Ok("Нет текущего трека");

		return CommandResult.Ok($"Текущий трек: {current}");
	}
}

/// <summary>clear — очистить строку результата.</summary>
public sealed class ClearCommand : ICommand
{
	public string Name => "clear";
	public string Description => "clear — очистить строку результата.";
	public IReadOnlyList<string> Aliases => [];

	public CommandResult Execute(string[] args)
		=> CommandResult.Ok(" ");
}
