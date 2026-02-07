namespace YamBassPlayer.Views;

public interface IEqualizerView
{
	event Action? OnOkClicked;
	event Action? OnCancelClicked;
	event Action<int, float>? OnBandChanged;
	
	void SetBandValue(int bandIndex, float value);
	float[] GetBandValues();
	void Show();
	void Close();
}
