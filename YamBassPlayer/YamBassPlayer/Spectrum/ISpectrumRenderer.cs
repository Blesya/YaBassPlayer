using Terminal.Gui;
using YamBassPlayer.Enums;

namespace YamBassPlayer.Spectrum;

public interface ISpectrumRenderer
{
    string DisplayName { get; }
    SpectrumDataType DataType { get; }
    void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz);
    void Reset();
}
