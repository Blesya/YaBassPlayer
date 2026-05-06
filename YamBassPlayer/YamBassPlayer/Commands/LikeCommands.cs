using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Commands;

/// <summary>
/// Базовый класс команд «избранное». Проверяет наличие текущего трека,
/// применимость источника и публикует <see cref="LikeCommandEvent"/>,
/// который обрабатывается в MainWindowCoordinator (асинхронно).
/// </summary>
public abstract class LikeCommandBase(
	IPlaybackQueue playbackQueue,
	ITrackSourceDetector sourceDetector,
	IEventBus eventBus) : ICommand
{
	protected abstract string SourceId { get; }
	protected abstract string SourceName { get; }
	protected abstract string SuccessMessage { get; }
	protected abstract bool IsApplicable(string trackId);

	public abstract string Name { get; }
	public abstract string Description { get; }
	public abstract IReadOnlyList<string> Aliases { get; }

	public CommandResult Execute(string[] args)
	{
		string? trackId = playbackQueue.CurrentTrackId;
		if (string.IsNullOrWhiteSpace(trackId))
			return CommandResult.Error("Нет текущего трека. Сначала начните воспроизведение");

		if (!IsApplicable(trackId))
			return CommandResult.Error($"Текущий трек нельзя добавить в {SourceName}");

		eventBus.Publish(new LikeCommandEvent(SourceId, trackId));
		return CommandResult.Ok(SuccessMessage);
	}
}

/// <summary>likeyandex — добавить/убрать текущий трек в избранное Яндекс.Музыки.</summary>
public sealed class LikeYandexCommand(
	IPlaybackQueue playbackQueue,
	ITrackSourceDetector sourceDetector,
	IEventBus eventBus) : LikeCommandBase(playbackQueue, sourceDetector, eventBus)
{
	public override string Name => "likeyandex";
	public override string Description => "likeyandex|ly — добавить/убрать текущий трек в избранное Яндекс.Музыки.";
	public override IReadOnlyList<string> Aliases => ["ly"];

	protected override string SourceId => SourceIds.Yandex;
	protected override string SourceName => "избранное Яндекс.Музыки";
	protected override string SuccessMessage => "Добавлено в Мои Треки";
	protected override bool IsApplicable(string trackId) => !sourceDetector.IsLocal(trackId);
}

/// <summary>likelocal — добавить/убрать текущий трек в локальное избранное.</summary>
public sealed class LikeLocalCommand(
	IPlaybackQueue playbackQueue,
	ITrackSourceDetector sourceDetector,
	IEventBus eventBus) : LikeCommandBase(playbackQueue, sourceDetector, eventBus)
{
	public override string Name => "likelocal";
	public override string Description => "likelocal|ll — добавить/убрать текущий трек в локальное избранное.";
	public override IReadOnlyList<string> Aliases => ["ll"];

	protected override string SourceId => SourceIds.Local;
	protected override string SourceName => "локальное избранное";
	protected override string SuccessMessage => "Добавлено в локальное избранное!";
	protected override bool IsApplicable(string trackId) => true;
}
