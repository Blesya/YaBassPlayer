using YamBassPlayer.Services;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public class EqualizerPresenter : IEqualizerPresenter
{
    private readonly IBassEqualizer _bassEqualizer;
    private readonly float[] _savedValues = new float[10];
    private readonly float[] _tempValues = new float[10];

    public EqualizerPresenter(IBassEqualizer bassEqualizer)
    {
        _bassEqualizer = bassEqualizer;
    }

    public void ShowEqualizerDialog()
    {
        var view = new EqualizerView();
        
        Array.Copy(_savedValues, _tempValues, 10);
        
        for (int i = 0; i < 10; i++)
        {
            view.SetBandValue(i, _savedValues[i]);
        }

        view.OnBandChanged += (bandIndex, value) =>
        {
            _tempValues[bandIndex] = value;
            ApplyEqualizerValues(_tempValues);
        };

        view.OnOkClicked += () =>
        {
            Array.Copy(_tempValues, _savedValues, 10);
        };

        view.OnCancelClicked += () =>
        {
            ApplyEqualizerValues(_savedValues);
        };

        view.Show();
    }

    private void ApplyEqualizerValues(float[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            _bassEqualizer.SetBand(i, values[i]);
        }
    }
}
