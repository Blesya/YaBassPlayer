using Terminal.Gui;
using YamBassPlayer.Enums;

namespace YamBassPlayer.Spectrum;

public interface ISpectrumRenderer
{
    string DisplayName { get; }
    SpectrumDataType DataType { get; }
    void Render(Rect bounds, ISpectrumCanvas canvas, float[] data, int maxFrequencyHz);
    void Reset();
}
