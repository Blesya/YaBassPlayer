using YamBassPlayer.Enums;

namespace YamBassPlayer.Services;

public interface IPlaybackCoordinator
{
	void SetPlaylistType(PlaylistType playlistType);
	void MarkMyWaveSkipPending();
	Task PlaySelectedTrackAsync(string trackId);
	Task PreloadNextTrackAsync();
}
