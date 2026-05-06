using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class Tunnel3DRenderer : ISpectrumRenderer
{
    public string DisplayName => "▣ Тоннель";
    public SpectrumDataType DataType => SpectrumDataType.Fft;

    private float[][] _history = null!;
    private int _levels;
    private int _writeIdx;
    private int _lastDataLen;

    public void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz)
    {
        int w = bounds.Width, h = bounds.Height;
        if (w < 4 || h < 4 || data.Length == 0) return;

        int levels = Math.Min(8, Math.Min(w, h) / 3);
        if (levels < 2) levels = 2;

        float ratio = Math.Clamp(maxFrequencyHz / 22050f, 0.05f, 1f);
        int usableBins = Math.Max(1, (int)(data.Length * ratio));

        if (_history == null || _levels != levels || _lastDataLen != usableBins)
        {
            _levels = levels;
            _lastDataLen = usableBins;
            _history = new float[levels][];
            for (int i = 0; i < levels; i++)
                _history[i] = new float[usableBins];
            _writeIdx = 0;
        }

        // store current frame (resampled to usableBins)
        float[] currentRow = _history[_writeIdx];
        for (int i = 0; i < usableBins; i++)
        {
            int srcIdx = i * data.Length / usableBins;
            srcIdx = Math.Clamp(srcIdx, 0, data.Length - 1);
            currentRow[i] = data[srcIdx];
        }
        _writeIdx = (_writeIdx + 1) % _levels;

        // draw from outermost (newest) to innermost (oldest)
        for (int l = 0; l < _levels; l++)
        {
            int bufIdx = (_writeIdx - 1 - l + _levels) % _levels;
            float[] frame = _history[bufIdx];

            float t = l / (float)(_levels - 1);
            float sizeFrac = 1.0f - t * 0.55f;

            int rw = Math.Max(3, (int)(w * sizeFrac));
            int rh = Math.Max(3, (int)(h * sizeFrac));
            int left = (w - rw) / 2;
            int top = (h - rh) / 2;
            int right = left + rw - 1;
            int bottom = top + rh - 1;

            int perimeter = 2 * (rw + rh);
            if (perimeter <= 0) continue;

            // top edge (L→R)
            for (int x = left; x <= right; x++)
            {
                int pos = x - left;
                int bin = pos * frame.Length / perimeter;
                bin = Math.Clamp(bin, 0, frame.Length - 1);
                DrawAt(driver, host, x, top, frame[bin]);
            }
            // right edge (T→B), excluding corners
            for (int y = top + 1; y < bottom; y++)
            {
                int pos = rw + (y - top);
                int bin = pos * frame.Length / perimeter;
                bin = Math.Clamp(bin, 0, frame.Length - 1);
                DrawAt(driver, host, right, y, frame[bin]);
            }
            // bottom edge (R→L)
            for (int x = right - 1; x >= left; x--)
            {
                int pos = rw + rh + (right - 1 - x);
                int bin = pos * frame.Length / perimeter;
                bin = Math.Clamp(bin, 0, frame.Length - 1);
                DrawAt(driver, host, x, bottom, frame[bin]);
            }
            // left edge (B→T), excluding corners
            for (int y = bottom - 1; y > top; y--)
            {
                int pos = rw + rh + rw + (bottom - 1 - y);
                int bin = pos * frame.Length / perimeter;
                bin = Math.Clamp(bin, 0, frame.Length - 1);
                DrawAt(driver, host, left, y, frame[bin]);
            }
        }
    }

    private static void DrawAt(ConsoleDriver driver, View host, int x, int y, float val)
    {
        if (val < 0.003f) return;

        char ch;
        Color color;

        switch (val)
        {
            case < 0.08f:
                ch = '░'; color = Color.Blue;
                break;
            case < 0.18f:
                ch = '░'; color = Color.Cyan;
                break;
            case < 0.30f:
                ch = '▒'; color = Color.BrightCyan;
                break;
            case < 0.45f:
                ch = '▒'; color = Color.Green;
                break;
            case < 0.60f:
                ch = '▓'; color = Color.BrightGreen;
                break;
            case < 0.78f:
                ch = '▓'; color = Color.BrightYellow;
                break;
            default:
                ch = '█'; color = Color.BrightRed;
                break;
        }

        driver.SetAttribute(new Attribute(color, Color.Black));
        host.Move(x, y);
        driver.AddRune(ch);
    }

    public void Reset()
    {
        _history = null!;
        _writeIdx = 0;
    }
}
