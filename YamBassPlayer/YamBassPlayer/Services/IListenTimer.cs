namespace YamBassPlayer.Services;

public interface IListenTimer
{
    void OnTrackStart(string trackId);
    void OnPause();
    void OnResume();
    void OnTrackStopOrChange();
}