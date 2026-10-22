using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

/// <summary>
/// Orchestrates playback: loading a selected track, refreshing the play-status
/// presenter and notifying the personal-radio («Моя волна») presenter.
/// Lives in the presenter layer (not the service layer) because it coordinates UI presenters.
/// </summary>
public interface IPlaybackPresenter
{
	void SetPlaylistContext(PlaylistContext context);
	void MarkMyWaveSkipPending();
	Task PlaySelectedTrackAsync(TrackKey key, bool isAutomatic = false);
	Task PreloadNextTrackAsync();
}
