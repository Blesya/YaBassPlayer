using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IMyWaveView
{
	Action? OnClose { get; set; }

	void SetTrack(Track track);
	void SetCover(string? coverPath);
	void SetListenCount(int count);
	void SetWaveDescription(string description);
	void SetNextTrackLabel(string? label);

	void Show();
	void Close();
}
