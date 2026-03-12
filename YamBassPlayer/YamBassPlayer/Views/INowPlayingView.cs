using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface INowPlayingView
{
	void SetTrack(Track track);
	void SetFftData(float[] fft);
	void SetListenCount(int count);
	void Show();
	void Close();
}
