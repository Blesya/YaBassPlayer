using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Commands;

/// <summary>queue|q - состояние текущей очереди.</summary>
public sealed class QueueCommand(IPlaybackQueue playbackQueue) : ICommand
{
	public string Name => "queue";
	public string Description => "queue|q - состояние текущей очереди.";
	public IReadOnlyList<string> Aliases => ["q"];

	public CommandResult Execute(string[] args)
	{
		var count = playbackQueue.TrackKeys.Count;
		TrackKey? current = playbackQueue.CurrentTrack;
		if (count == 0)
			return CommandResult.Ok("Очередь пуста");

		string suffix = current is null ? string.Empty : $" Текущий: {current.Value}";
		return CommandResult.Ok($"Треков в очереди: {count}.{suffix}");
	}
}
