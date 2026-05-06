using YamBassPlayer.Services;

namespace YamBassPlayer.Commands;

/// <summary>queue|q — показать состояние текущей очереди.</summary>
public sealed class QueueCommand(IPlaybackQueue playbackQueue) : ICommand
{
	public string Name => "queue";
	public string Description => "queue|q — состояние текущей очереди.";
	public IReadOnlyList<string> Aliases => ["q"];

	public CommandResult Execute(string[] args)
	{
		var count = playbackQueue.TrackIds.Count;
		var current = playbackQueue.CurrentTrackId;
		if (count == 0)
			return CommandResult.Ok("Очередь пуста");

		string suffix = string.IsNullOrEmpty(current) ? string.Empty : $" Текущий: {current}";
		return CommandResult.Ok($"Треков в очереди: {count}.{suffix}");
	}
}
