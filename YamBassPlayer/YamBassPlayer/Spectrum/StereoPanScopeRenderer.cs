using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class StereoPanScopeRenderer : ISpectrumRenderer
{
    public string DisplayName => "〰 Панорама";
    public SpectrumDataType DataType => SpectrumDataType.Waveform;

    private float[,] _persistence = null!;
    private int _pw, _ph;

    public void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz)
    {
        int w = bounds.Width, h = bounds.Height;
        if (w < 4 || h < 4 || data.Length < 4) return;

        if (_persistence == null || _pw != w || _ph != h)
        {
            _persistence = new float[w, h];
            _pw = w;
            _ph = h;
        }

        // fade persistence
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                _persistence[x, y] *= 0.88f;

        int pairs = data.Length / 2;
        int bins = Math.Min(48, w);   // one bin per column max
        int samplesPerBin = Math.Max(1, pairs / bins);
        bins = pairs / samplesPerBin; // actual bin count

        float cx = w / 2f;
        float halfH = (h - 1) / 2f;

        for (int b = 0; b < bins; b++)
        {
            float lSum = 0, rSum = 0;
            int count = 0;
            for (int s = 0; s < samplesPerBin; s++)
            {
                int idx = b * samplesPerBin + s;
                if (idx >= pairs) break;
                float l = data[idx * 2];
                float r = data[idx * 2 + 1];
                lSum += l * l;
                rSum += r * r;
                count++;
            }
            if (count == 0) continue;

            float lRms = MathF.Sqrt(lSum / count);
            float rRms = MathF.Sqrt(rSum / count);
            float total = lRms + rRms;

            if (total < 0.005f) continue;

            // pan: -1 (full left) to +1 (full right)
            float pan = (rRms - lRms) / total;

            int x = b * w / bins;
            int y = (int)(halfH + pan * halfH + 0.5);
            y = Math.Clamp(y, 0, h - 1);

            // accumulate with persistence
            _persistence[x, y] = Math.Min(1f, _persistence[x, y] + total * 2.5f);
        }

        // draw persistence grid
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float val = _persistence[x, y];
                if (val < 0.01f) continue;

                Color c = val switch
                {
                    < 0.20f => Color.DarkGray,
                    < 0.40f => Color.Cyan,
                    < 0.60f => Color.BrightCyan,
                    < 0.80f => Color.Green,
                    _ => Color.BrightGreen
                };
                driver.SetAttribute(new Attribute(c, Color.Black));
                host.Move(x, y);
                driver.AddRune('█');
            }
        }

        // center line
        int cyi = (int)halfH;
        driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
        for (int x = 0; x < w; x += 3)
        {
            host.Move(x, cyi);
            driver.AddRune('·');
        }
    }

    public void Reset()
    {
        if (_persistence != null)
            Array.Clear(_persistence, 0, _persistence.Length);
    }
}
