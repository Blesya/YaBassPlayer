using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Presenters;

public interface ITracksPresenter
{
	event Action<Track>? OnTrackChosen;
	IPlaybackQueue PlaybackQueue { get; }
	Task LoadTracksFor(Playlist playlist);
}