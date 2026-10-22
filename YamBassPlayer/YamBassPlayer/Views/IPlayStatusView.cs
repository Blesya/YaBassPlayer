using YamBassPlayer.Enums;

namespace YamBassPlayer.Views;

public interface IPlayStatusView
{
	event Action? OnPlayClicked;
	event Action? OnStopClicked;
	event Action? OnPrevClicked;
	event Action? OnNextClicked;
	event Action<int>? OnSeekRequested;
	event Action<int>? OnVolumeChangeRequested;
	void SetVolume(int percent);
	void SetPlayStatus(string status);
	void SetProgress(int percent);
	/// <summary>Состояние воспроизведения: кнопка показывает «▶» или «⏸».</summary>
	void SetPlaying(bool isPlaying);
	void SetTime(TimeSpan current, TimeSpan duration);
	void SetTitle(string title);
	/// <summary>Кнопки действий, объявленные плагинами для текущего трека.</summary>
	void SetTrackActions(IReadOnlyList<TrackActionButton> actions);
	event Action<string>? OnTrackActionClicked;
	event Action? OnQueueClicked;
	event Action? OnPlaybackModeToggled;
	event Action? OnRestartClicked;
	void SetPlaybackMode(PlaybackMode mode);
}
