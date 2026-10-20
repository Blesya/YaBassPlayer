using YamBassPlayer.Presenters;
using YamBassPlayer.Services;

namespace YamBassPlayer.Commands;

/// <summary>about — информация о текущем треке: доступность, метки, кредиты, описание исполнителя.</summary>
public sealed class AboutCommand(ITrackAboutPresenter trackAboutPresenter, IPlaybackQueue playbackQueue) : ICommand
{
	public string Name => "about";
	public string Description => "about|info — информация о текущем треке: доступность, метки, кредиты, описание исполнителя.";
	public IReadOnlyList<string> Aliases => ["info"];

	public CommandResult Execute(string[] args)
	{
		if (string.IsNullOrWhiteSpace(playbackQueue.CurrentTrackId))
			return CommandResult.Ok("Нет текущего трека");

		trackAboutPresenter.ShowCurrentTrackAbout();
		return CommandResult.Ok("О треке");
	}
}
