using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IPlayStatusPresenter
{
	event Action? OnPlayClicked;
	event Action? OnStopClicked;
	event Action? OnPrevClicked;
	event Action? OnNextClicked;
	event Action? OnQueueClicked;
	event Action? OnPlaybackModeToggled;
	event Action? OnRestartClicked;
	void SetPlayStatus(string status);
	void SetTitle(string title);
	void SetCurrentTrack(TrackKey? key);
	void SetPlaybackMode(PlaybackMode mode);
}
