using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ILargeTrackInfoView
{
	Action? OnClose { get; set; }
	void SetTrack(Track track);
	void SetListenCount(int count);
	void SetCover(string? coverPath);
	void Show();
	void Close();
}
