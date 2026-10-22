using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IListenTimer
{
	void OnTrackStart(TrackKey key, PlaylistContext context);
	void OnPause();
	void OnResume();
	void OnTrackStopOrChange();
}
