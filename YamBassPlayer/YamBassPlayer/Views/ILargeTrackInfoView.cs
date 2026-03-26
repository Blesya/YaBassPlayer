using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ILargeTrackInfoView
{
	Action? OnClose { get; set; }
	Action<string>? OnTrackActivated { get; set; }
	void SetTrack(Track track);
	void SetListenCount(int count);
	void SetCover(string? coverPath);
	void SetPlaylist(IReadOnlyList<Track> tracks);
	void SetCurrentTrackId(string? trackId);
	void Show();
	void Close();
}
