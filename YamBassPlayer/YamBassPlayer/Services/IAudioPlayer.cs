namespace YamBassPlayer.Services;

public interface IAudioPlayer
{
	event EventHandler? OnTrackEnded;
	event EventHandler? OnPreloadRequested;
	bool IsPlayed { get; }
	void Init();
	void Play(string filePath, string trackName = "");
	int GetProgressInPercent();
	TimeSpan GetCurrentPosition();
	TimeSpan GetDuration();
	float[] ChannelGetData();
	void SeekToPercent(int percent);
	void Pause();
	void Resume();
	void Stop();
	void Free();
	void SetEqualizerBand(int bandIndex, float gain);
}