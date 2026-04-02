using YamBassPlayer.Enums;

namespace YamBassPlayer.Views;

public interface IPlayStatusView
{
	event Action? OnPlayClicked;
	event Action? OnStopClicked;
	event Action? OnPrevClicked;
	event Action? OnNextClicked;
	event Action<int>? OnSeekRequested;
	event Action? OnLocalFavoriteToggleClicked;
	void SetPlayStatus(string status);
	void SetProgress(int percent);
	void SetTime(TimeSpan current, TimeSpan duration);
	void SetTitle(string title);
	void SetLocalFavoriteState(bool isFavorite);
	void SetLocalFavoriteVisibility(bool isVisible);
	void SetLocalFavoriteEnabled(bool isEnabled);
	event Action? OnYandexFavoriteToggleClicked;
	void SetYandexFavoriteState(bool isFavorite);
	void SetYandexFavoriteVisibility(bool isVisible);
	void SetYandexFavoriteEnabled(bool isEnabled);
	event Action? OnQueueClicked;
	event Action? OnPlaybackModeToggled;
	void SetPlaybackMode(PlaybackMode mode);
}
