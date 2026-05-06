using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Spectrum;

namespace YamBassPlayer.Views.Impl;

public sealed class SpectrumView : View
{
    private readonly List<ISpectrumRenderer> _renderers = new();
    private int _currentIndex;
    private ISpectrumRenderer CurrentRenderer => _renderers[_currentIndex];
    private float[] _data = [];

    public int Bars { get; }
    public int MaxFrequencyHz { get; set; } = 22050;
    public SpectrumDataType RequiredDataType => CurrentRenderer.DataType;
    public string ModeDisplayName => CurrentRenderer.DisplayName;
    public int ModeCount => _renderers.Count;

    public SpectrumView(int bars = 25)
    {
        Bars = bars;
        CanFocus = false;
    }

    public void AddRenderer(ISpectrumRenderer renderer)
    {
        _renderers.Add(renderer);
    }

    public void SetData(float[] data)
    {
        _data = data;
        SetNeedsDisplay();
    }

    public void CycleMode()
    {
        if (_renderers.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _renderers.Count;
        CurrentRenderer.Reset();
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);

        if (_renderers.Count == 0)
            return;

        CurrentRenderer.Render(bounds, Application.Driver, this, _data, MaxFrequencyHz);
    }
}
