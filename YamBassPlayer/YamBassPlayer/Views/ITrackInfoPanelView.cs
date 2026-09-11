using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Views;

public interface ITrackInfoPanelView
{
	(int Width, int Height) CoverSize { get; }
	void SetTrack(Track track);
	void SetListenCount(int count);
	void SetCover(CoverArt? art);
	void SetLyrics(string? lyrics);
	void ClearTrack();
}
