using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Commands;

/// <summary>now - показать текущий трек.</summary>
public sealed class NowCommand(IPlaybackQueue playbackQueue) : ICommand
{
	public string Name => "now";
	public string Description => "now - текущий трек.";
	public IReadOnlyList<string> Aliases => ["track"];

	public CommandResult Execute(string[] args)
	{
		TrackKey? current = playbackQueue.CurrentTrack;
		if (current is null)
			return CommandResult.Ok("Нет текущего трека");

		return CommandResult.Ok($"Текущий трек: {current.Value}");
	}
}

/// <summary>clear - очистить строку вывода.</summary>
public sealed class ClearCommand : ICommand
{
	public string Name => "clear";
	public string Description => "clear - очистить строку вывода.";
	public IReadOnlyList<string> Aliases => [];

	public CommandResult Execute(string[] args)
		=> CommandResult.Ok(" ");
}
