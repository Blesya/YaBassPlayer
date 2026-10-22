namespace YamBassPlayer.Services;

public interface IAudioPlayer
{
    event EventHandler? OnTrackEnded;
    event EventHandler? OnPreloadRequested;
    bool IsPlayed { get; }
    /// <summary>Текущая громкость в процентах, 0..100.</summary>
    int VolumePercent { get; }
    void Init();
    void Play(string filePath, string trackName = "");
    int GetProgressInPercent();
    TimeSpan GetCurrentPosition();
    TimeSpan GetDuration();
    float[] ChannelGetData();
    float[] GetWaveformData(int sampleCount = 512);
    void SeekToPercent(int percent);
    /// <summary>Устанавливает громкость в процентах (0..100); значение сохраняется в конфиг.</summary>
    void SetVolumePercent(int percent);
    void Pause();
    void Resume();
    void Stop();
    void Free();
    void SetEqualizerBand(int bandIndex, float gain);
}
