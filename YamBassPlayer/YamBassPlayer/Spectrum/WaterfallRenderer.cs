using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class WaterfallRenderer : ISpectrumRenderer
{
    public string DisplayName => "▦ Водопад";
    public SpectrumDataType DataType => SpectrumDataType.Fft;

    private float[][] _buffer = null!;
    private int _rows;
    private int _cols;
    private int _writeIdx;

    public void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz)
    {
        int w = bounds.Width, h = bounds.Height;
        if (w < 2 || h < 2 || data.Length == 0) return;

        if (_buffer == null || _cols != w || _rows != h)
        {
            _rows = h;
            _cols = w;
            _buffer = new float[h][];
            for (int i = 0; i < h; i++)
                _buffer[i] = new float[w];
            _writeIdx = 0;
        }

        float ratio = Math.Clamp(maxFrequencyHz / 22050f, 0.05f, 1f);
        int usableBins = Math.Max(1, (int)(data.Length * ratio));

        float[] row = new float[w];
        for (int x = 0; x < w; x++)
        {
            int binIdx = Math.Clamp((int)(x / (float)w * usableBins), 0, data.Length - 1);
            float val = data[binIdx] * ((float)Math.Log2(x + 2) * 2f);
            row[x] = Math.Clamp(val, 0f, 1f);
        }

        Array.Copy(row, _buffer[_writeIdx], _cols);
        _writeIdx = (_writeIdx + 1) % _rows;

        for (int r = 0; r < _rows; r++)
        {
            int bufIdx = (_writeIdx + r) % _rows;
            var bufRow = _buffer[bufIdx];

            for (int x = 0; x < _cols; x++)
            {
                float val = bufRow[x];
                if (val < 0.003f) continue;

                char ch;
                Color color;

                if (val < 0.08f) { ch = '░'; color = Color.Blue; }
                else if (val < 0.20f) { ch = '░'; color = Color.Cyan; }
                else if (val < 0.35f) { ch = '▒'; color = Color.BrightCyan; }
                else if (val < 0.50f) { ch = '▒'; color = Color.Green; }
                else if (val < 0.65f) { ch = '▓'; color = Color.BrightGreen; }
                else if (val < 0.80f) { ch = '▓'; color = Color.BrightYellow; }
                else { ch = '█'; color = Color.BrightRed; }

                driver.SetAttribute(new Attribute(color, Color.Black));
                host.Move(x, r);
                driver.AddRune(ch);
            }
        }
    }

    public void Reset()
    {
        if (_buffer != null)
            for (int i = 0; i < _buffer.Length; i++)
                Array.Clear(_buffer[i], 0, _buffer[i].Length);
        _writeIdx = 0;
    }
}
