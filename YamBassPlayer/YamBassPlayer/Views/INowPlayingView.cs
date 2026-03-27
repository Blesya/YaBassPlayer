using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface INowPlayingView
{
	SpectrumMode Mode { get; }
	void SetTrack(Track track);
	void SetFftData(float[] fft);
	void SetWaveformData(float[] samples);
	void SetListenCount(int count);
	void Show();
	void Close();
}
