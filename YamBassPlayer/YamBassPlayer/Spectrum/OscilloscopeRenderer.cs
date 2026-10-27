using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class OscilloscopeRenderer : ISpectrumRenderer
{
    public string DisplayName => "〜 Осц.";
    public SpectrumDataType DataType => SpectrumDataType.Waveform;

    // Буферы кадра: переиспользуются, иначе на 60 кадрах в секунду получалось
    // несколько килобайт мусора в gen0 каждую секунду.
    private float[] _resampled = [];
    private float[] _smoothed = [];
    private float[] _lowPassed = [];

    public void Render(Rect bounds, ISpectrumCanvas canvas, float[] data, int maxFrequencyHz)
    {
        int width = bounds.Width;
        int height = bounds.Height;
        int midY = height / 2;

        if (data.Length == 0)
            return;

        canvas.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
        for (int x = 0; x < width; x++)
        {
            canvas.Move(x, midY);
            canvas.AddRune('─');
        }

        float[] filtered = ApplyLowPass(data, maxFrequencyHz);
        float[] smoothed = SmoothSamples(filtered, width, windowSize: 5);

        int halfHeight = height * 2;
        int midHalf = halfHeight / 2;

        int prevHY = midHalf - (int)(smoothed[0] * midHalf);
        prevHY = Math.Clamp(prevHY, 0, halfHeight - 1);

        for (int x = 0; x < width; x++)
        {
            float sample = smoothed[x];
            int hy = midHalf - (int)(sample * midHalf);
            hy = Math.Clamp(hy, 0, halfHeight - 1);

            Color pointColor = Math.Abs(sample) switch
            {
                < 0.10f => Color.BrightCyan,
                < 0.25f => Color.Cyan,
                < 0.45f => Color.Green,
                < 0.65f => Color.BrightGreen,
                < 0.80f => Color.BrightYellow,
                < 0.92f => Color.BrightRed,
                _ => Color.Red
            };

            int hyTop = Math.Min(prevHY, hy);
            int hyBottom = Math.Max(prevHY, hy);
            DrawVerticalSegment(canvas, x, hyTop, hyBottom, halfHeight, pointColor);

            prevHY = hy;
        }
    }

    public void Reset() { }

    private float[] SmoothSamples(float[] waveform, int targetWidth, int windowSize)
    {
        if (_resampled.Length != targetWidth)
        {
            _resampled = new float[targetWidth];
        }

        if (_smoothed.Length != targetWidth)
        {
            _smoothed = new float[targetWidth];
        }

        float[] resampled = _resampled;
        for (int x = 0; x < targetWidth; x++)
        {
            float t = (float)x / Math.Max(1, targetWidth - 1);
            int idx = Math.Clamp((int)(t * (waveform.Length - 1)), 0, waveform.Length - 1);
            resampled[x] = waveform[idx];
        }

        float[] result = _smoothed;
        int half = windowSize / 2;
        for (int x = 0; x < targetWidth; x++)
        {
            float sum = 0f;
            int count = 0;
            for (int d = -half; d <= half; d++)
            {
                int nx = x + d;
                if (nx >= 0 && nx < targetWidth)
                {
                    sum += resampled[nx];
                    count++;
                }
            }
            result[x] = sum / count;
        }
        return result;
    }

    private float[] ApplyLowPass(float[] samples, int maxFreqHz)
    {
        if (maxFreqHz >= 22050)
            return samples;

        if (_lowPassed.Length != samples.Length)
        {
            _lowPassed = new float[samples.Length];
        }

        float alpha = Math.Clamp(maxFreqHz / 22050f, 0.01f, 1f);
        float[] output = _lowPassed;
        output[0] = samples[0];
        for (int i = 1; i < samples.Length; i++)
            output[i] = output[i - 1] + alpha * (samples[i] - output[i - 1]);
        return output;
    }

    private static void DrawVerticalSegment(ISpectrumCanvas canvas, int x, int yTop, int yBottom, int height, Color color)
    {
        canvas.SetAttribute(new Attribute(color, Color.Black));

        if (yTop == yBottom)
        {
            int charRow = yTop / 2;
            bool isTopHalf = (yTop % 2 == 0);
            canvas.Move(x, charRow);
            canvas.AddRune(isTopHalf ? '▀' : '▄');
            return;
        }

        int firstCharRow = yTop / 2;
        int lastCharRow = yBottom / 2;

        for (int row = firstCharRow; row <= lastCharRow && row < (height + 1) / 2; row++)
        {
            int halfTop = row * 2;
            int halfBottom = row * 2 + 1;

            bool topFilled = halfTop >= yTop && halfTop <= yBottom;
            bool bottomFilled = halfBottom >= yTop && halfBottom <= yBottom;

            char glyph = (topFilled, bottomFilled) switch
            {
                (true, true) => '█',
                (true, false) => '▀',
                (false, true) => '▄',
                _ => ' '
            };

            if (glyph != ' ')
            {
                canvas.Move(x, row);
                canvas.AddRune(glyph);
            }
        }
    }
}
