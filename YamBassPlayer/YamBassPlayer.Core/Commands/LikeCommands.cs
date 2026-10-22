using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Commands;

/// <summary>
/// Команда «избранное»: проверяет наличие текущего трека и публикует
/// <see cref="LikeCommandEvent"/>, который обрабатывается в MainWindowCoordinator.
/// Избранное одно и хранится локально, поэтому источник трека не важен.
/// </summary>
public abstract class LikeCommandBase(
	IPlaybackQueue playbackQueue,
	IEventBus eventBus) : ICommand
{
	protected abstract string SuccessMessage { get; }

	public abstract string Name { get; }
	public abstract string Description { get; }
	public abstract IReadOnlyList<string> Aliases { get; }

	public CommandResult Execute(string[] args)
	{
		if (playbackQueue.CurrentTrack is not { } key)
			return CommandResult.Error("Нет текущего трека. Сначала начните воспроизведение");

		eventBus.Publish(new LikeCommandEvent(key));
		return CommandResult.Ok(SuccessMessage);
	}
}

/// <summary>likelocal — добавить/убрать текущий трек в локальное избранное.</summary>
public sealed class LikeLocalCommand(
	IPlaybackQueue playbackQueue,
	IEventBus eventBus) : LikeCommandBase(playbackQueue, eventBus)
{
	public override string Name => "likelocal";
	public override string Description => "likelocal|ll — добавить/убрать текущий трек в локальное избранное.";
	public override IReadOnlyList<string> Aliases => ["ll"];

	protected override string SuccessMessage => "Добавлено в локальное избранное!";
}
