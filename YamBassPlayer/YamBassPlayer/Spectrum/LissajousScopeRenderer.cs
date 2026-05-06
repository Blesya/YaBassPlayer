using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class LissajousScopeRenderer : ISpectrumRenderer
{
    public string DisplayName => "⊞ Лиссаж.";
    public SpectrumDataType DataType => SpectrumDataType.Waveform;

    public void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz)
    {
        int w = bounds.Width, h = bounds.Height;
        if (w < 4 || h < 4 || data.Length < 4) return;

        float cx = (w - 1) / 2f, cy = (h - 1) / 2f;
        float scale = Math.Min(cx, cy) * 0.85f;

        driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
        int cxi = (int)cx, cyi = (int)cy;
        for (int x = cxi - 2; x <= cxi + 2; x += 4)
            if (x >= 0 && x < w) { host.Move(x, cyi); driver.AddRune('·'); }
        for (int y = cyi - 1; y <= cyi + 1; y += 2)
            if (y >= 0 && y < h) { host.Move(cxi, y); driver.AddRune('·'); }

        int pairs = data.Length / 2;
        bool[,] hits = new bool[w, h];

        for (int i = 0; i < pairs; i++)
        {
            float l = data[i * 2];
            float r = data[i * 2 + 1];
            int x = (int)(cx + l * scale + 0.5);
            int y = (int)(cy + r * scale + 0.5);
            if (x >= 0 && x < w && y >= 0 && y < h)
                hits[x, y] = true;
        }

        driver.SetAttribute(new Attribute(Color.BrightCyan, Color.Black));
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (hits[x, y])
                {
                    host.Move(x, y);
                    driver.AddRune('█');
                }
    }

    public void Reset() { }
}
