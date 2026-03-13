using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ITrackInfoPanelView
{
	void SetTrack(Track track);
	void SetListenCount(int count);
	void SetCover(string? coverPath);
	void SetLyrics(string? lyrics);
	void ClearTrack();
}
