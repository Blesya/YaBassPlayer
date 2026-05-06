using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class RingsRenderer : ISpectrumRenderer
{
    public string DisplayName => "◎ Радар";
    public SpectrumDataType DataType => SpectrumDataType.Fft;

    private float[,] _grid = null!;
    private int _gw, _gh;

    public void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz)
    {
        int w = bounds.Width, h = bounds.Height;
        if (w < 4 || h < 4 || data.Length == 0) return;

        float cx = (w - 1) / 2f, cy = (h - 1) / 2f;
        float maxR = Math.Min(cx, cy);
        const float xAspect = 2.2f;

        float ratio = Math.Clamp(maxFrequencyHz / 22050f, 0.05f, 1f);
        int usableBins = Math.Max(1, (int)(data.Length * ratio));

        if (_grid == null || _gw != w || _gh != h)
        {
            _grid = new float[w, h];
            _gw = w;
            _gh = h;
        }
        Array.Clear(_grid, 0, _grid.Length);

        // reference rings
        driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
        for (int ring = 1; ring <= 3; ring++)
        {
            float rr = maxR * ring / 4f;
            for (int a = 0; a < 360; a += 8)
            {
                double rad = a * Math.PI / 180.0;
                int px = (int)(cx + Math.Cos(rad) * rr * xAspect + 0.5);
                int py = (int)(cy + Math.Sin(rad) * rr + 0.5);
                if (px >= 0 && px < w && py >= 0 && py < h)
                {
                    host.Move(px, py);
                    driver.AddRune('·');
                }
            }
        }

        // radial guidelines every 45°
        for (int a = 0; a < 360; a += 45)
        {
            double rad = a * Math.PI / 180.0;
            for (float r = 0; r <= maxR * 0.95f; r += 0.5f)
            {
                int px = (int)(cx + Math.Cos(rad) * r * xAspect + 0.5);
                int py = (int)(cy + Math.Sin(rad) * r + 0.5);
                if (px >= 0 && px < w && py >= 0 && py < h)
                {
                    host.Move(px, py);
                    driver.AddRune('·');
                }
            }
        }

        // accumulate FFT hits on grid
        for (int i = 0; i < usableBins; i++)
        {
            double angle = Math.PI / 2 + i / (double)usableBins * Math.PI * 2;
            float radius = maxR * 0.15f + data[i] * maxR * 0.75f;
            radius = Math.Clamp(radius, 0f, maxR * 0.92f);

            int px = (int)(cx + Math.Cos(angle) * radius * xAspect + 0.5);
            int py = (int)(cy + Math.Sin(angle) * radius + 0.5);
            if (px >= 0 && px < w && py >= 0 && py < h && data[i] > _grid[px, py])
                _grid[px, py] = data[i];
        }

        // draw grid
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float val = _grid[x, y];
                if (val < 0.003f) continue;

                Color c = val switch
                {
                    < 0.15f => Color.Blue,
                    < 0.30f => Color.BrightBlue,
                    < 0.45f => Color.Cyan,
                    < 0.60f => Color.BrightCyan,
                    < 0.75f => Color.Green,
                    < 0.88f => Color.BrightGreen,
                    < 0.96f => Color.BrightYellow,
                    _ => Color.BrightRed
                };
                driver.SetAttribute(new Attribute(c, Color.Black));
                host.Move(x, y);
                driver.AddRune('▓');
            }
        }

        // center dot
        int cxi = (int)cx, cyi = (int)cy;
        if (cxi >= 0 && cxi < w && cyi >= 0 && cyi < h)
        {
            driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
            host.Move(cxi, cyi);
            driver.AddRune('+');
        }
    }

    public void Reset()
    {
        if (_grid != null)
            Array.Clear(_grid, 0, _grid.Length);
    }
}
