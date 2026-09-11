using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Views;

public interface ILargeTrackInfoView
{
	Action? OnClose { get; set; }
	Action<string>? OnTrackActivated { get; set; }
	(int Width, int Height) CoverSize { get; }
	void SetTrack(Track track);
	void SetListenCount(int count);
	void SetCover(CoverArt? art);
	void SetPlaylist(IReadOnlyList<Track> tracks);
	void SetCurrentTrackId(string? trackId);
	void Show();
	void Close();
}
