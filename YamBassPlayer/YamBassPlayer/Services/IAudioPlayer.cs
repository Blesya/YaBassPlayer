namespace YamBassPlayer.Services;

public interface IAudioPlayer
{
	event EventHandler? OnTrackEnded;
	bool IsPlayed { get; }
	void Init();
	void Play(string filePath, string trackName = "");
	int GetProgressInPercent();
	float[] ChannelGetData();
	void SeekToPercent(int percent);
	void Pause();
	void Resume();
	void Stop();
	void Free();
}