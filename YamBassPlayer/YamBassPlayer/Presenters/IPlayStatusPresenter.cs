using YamBassPlayer.Enums;

namespace YamBassPlayer.Presenters;

public interface IPlayStatusPresenter
{
	event Action? OnPlayClicked;
	event Action? OnStopClicked;
	event Action? OnPrevClicked;
	event Action? OnNextClicked;
	event Action? OnQueueClicked;
	event Action? OnPlaybackModeToggled;
	void SetPlayStatus(string status);
	void SetTitle(string title);
	void SetCurrentTrack(string? trackId, string? sourceType);
	void SetPlaybackMode(PlaybackMode mode);
}
