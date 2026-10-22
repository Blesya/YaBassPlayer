namespace YamBassPlayer.Services
{
	public interface IBassEqualizer
	{
		void AttachToStream(int streamHandle);
		void SetBand(int index, float gain);
		float[] GetBands();
	}
}
