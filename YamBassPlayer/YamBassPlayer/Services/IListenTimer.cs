using YamBassPlayer.Enums;

namespace YamBassPlayer.Services;

public interface IListenTimer
{
	void OnTrackStart(string trackId, ListenSource source);
	void OnPause();
	void OnResume();
	void OnTrackStopOrChange();
}