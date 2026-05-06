using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class BarsRenderer : ISpectrumRenderer
{
    public string DisplayName => "≋ FFT";
    public SpectrumDataType DataType => SpectrumDataType.Fft;
    public int BarWidth { get; set; } = 1;
    public int BarGap { get; set; } = 0;

    private readonly float[] _smoothed;
    private readonly float[] _peaks;
    private readonly float[] _peakFallSpeed;
    private readonly int _bars;

    public BarsRenderer(int bars)
    {
        _bars = bars;
        _smoothed = new float[bars];
        _peaks = new float[bars];
        _peakFallSpeed = new float[bars];
        var rnd = new Random();
        for (int i = 0; i < bars; i++)
            _peakFallSpeed[i] = 0.05f + (float)rnd.NextDouble() * 0.1f;
    }

    public void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz)
    {
        if (data.Length == 0) return;

        int height = bounds.Height;
        int cellWidth = BarWidth + BarGap;
        int numBars = Math.Min(_bars, bounds.Width / Math.Max(1, cellWidth));

        if (numBars <= 0) return;

        float ratio = Math.Clamp(maxFrequencyHz / 22050f, 0.05f, 1f);
        int usableBins = Math.Max(1, (int)(data.Length * ratio));
        float fftStepF = (float)usableBins / numBars;

        for (int i = 0; i < numBars; i++)
        {
            int start = (int)(i * fftStepF);
            int end = Math.Max(start + 1, (int)((i + 1) * fftStepF));
            end = Math.Min(end, usableBins);

            var rawValue = 0f;
            for (int j = start; j < end; j++)
                rawValue += data[j];
            rawValue /= (end - start);

            float k = ((float)Math.Log2(i + 1.3d)) * 10f;
            rawValue *= k;
            rawValue = Math.Clamp(rawValue, 0f, 1f);

            _smoothed[i] = _smoothed[i] * 0.7f + rawValue * 0.3f;
            float barHeight = _smoothed[i] * height;
            int barPixels = Math.Clamp((int)barHeight, 0, height);

            if (barHeight > _peaks[i])
                _peaks[i] = barHeight;
            else
            {
                _peaks[i] -= _peakFallSpeed[i];
                if (_peaks[i] < 0)
                    _peaks[i] = 0;
            }

            float t = _smoothed[i];

            Color barColor = t switch
            {
                < 0.15f => Color.Blue,
                < 0.30f => Color.BrightBlue,
                < 0.45f => Color.Cyan,
                < 0.60f => Color.BrightCyan,
                < 0.75f => Color.Green,
                < 0.85f => Color.BrightGreen,
                < 0.95f => Color.BrightYellow,
                _ => Color.BrightRed
            };

            driver.SetAttribute(new Attribute(barColor, Color.Black));

            int xStart = i * cellWidth;
            for (int y = 0; y < barPixels; y++)
            {
                for (int dx = 0; dx < BarWidth; dx++)
                {
                    host.Move(xStart + dx, height - 1 - y);
                    driver.AddRune('█');
                }
            }

            int peakY = height - 1 - (int)_peaks[i];
            if (peakY >= 0 && peakY < height)
            {
                driver.SetAttribute(new Attribute(Color.White, Color.Black));
                for (int dx = 0; dx < BarWidth; dx++)
                {
                    host.Move(xStart + dx, peakY);
                    driver.AddRune('░');
                }
            }
        }
    }

    public void Reset()
    {
        Array.Clear(_smoothed, 0, _smoothed.Length);
        Array.Clear(_peaks, 0, _peaks.Length);
    }
}
