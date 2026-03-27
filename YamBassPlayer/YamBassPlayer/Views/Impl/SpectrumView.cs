using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Views.Impl;

public sealed class SpectrumView : View
{
    public int Bars { get; }
    public int BarWidth { get; set; } = 1;
    public int BarGap { get; set; } = 0;
    public SpectrumMode Mode { get; set; } = SpectrumMode.Bars;

    private float[] _fft = new float[128];
    private float[] _waveform = [];
    private readonly float[] _smoothed;
    private readonly float[] _peaks;
    private readonly float[] _peakFallSpeed;
    private readonly Random _rnd = new();

    public SpectrumView(int bars = 25)
    {
        Bars = bars;
        _smoothed = new float[Bars];
        _peaks = new float[Bars];
        _peakFallSpeed = new float[Bars];
        for (int i = 0; i < Bars; i++)
            _peakFallSpeed[i] = 0.05f + (float)_rnd.NextDouble() * 0.1f;
        CanFocus = false;
    }

    public void SetFftData(float[] fft)
    {
        if (fft.Length == 0)
            return;
        _fft = fft;
        SetNeedsDisplay();
    }

    public void SetWaveformData(float[] samples)
    {
        _waveform = samples;
        SetNeedsDisplay();
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);

        if (Mode == SpectrumMode.Oscilloscope)
        {
            DrawOscilloscope(bounds);
            return;
        }

        DrawBars(bounds);
    }

    private void DrawOscilloscope(Rect bounds)
    {
        var driver = Application.Driver;
        int width = bounds.Width;
        int height = bounds.Height;
        int midY = height / 2;

        if (_waveform.Length == 0)
            return;

        driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
        for (int x = 0; x < width; x++)
        {
            Move(x, midY);
            driver.AddRune('─');
        }

        float[] smoothed = SmoothSamples(_waveform, width, windowSize: 5);

        // Работаем в пространстве полу-строк (0..halfHeight*2-1), чтобы
        // DrawVerticalSegment мог корректно делить на 2 для символьных строк.
        int halfHeight = height * 2; // total half-rows
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
            DrawVerticalSegment(driver, x, hyTop, hyBottom, halfHeight, pointColor);

            prevHY = hy;
        }
    }

    // Ресэмплирует waveform до targetWidth точек и применяет скользящее среднее.
    private static float[] SmoothSamples(float[] waveform, int targetWidth, int windowSize)
    {
        float[] resampled = new float[targetWidth];
        for (int x = 0; x < targetWidth; x++)
        {
            float t = (float)x / Math.Max(1, targetWidth - 1);
            int idx = Math.Clamp((int)(t * (waveform.Length - 1)), 0, waveform.Length - 1);
            resampled[x] = waveform[idx];
        }

        float[] result = new float[targetWidth];
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

    // Рисует вертикальный отрезок в столбце x от yTop до yBottom используя
    // полу-блочные символы (▀/▄/█) для двойного вертикального разрешения.
    private void DrawVerticalSegment(ConsoleDriver driver, int x, int yTop, int yBottom, int height, Color color)
    {
        driver.SetAttribute(new Attribute(color, Color.Black));

        // Расширяем до минимум одной строки, чтобы точка всегда была видна.
        if (yTop == yBottom)
        {
            // Определяем полу-строку: чётный y → верхняя половина символьной строки
            int charRow = yTop / 2;
            bool isTopHalf = (yTop % 2 == 0);
            Move(x, charRow);
            driver.AddRune(isTopHalf ? '▀' : '▄');
            return;
        }

        // Для каждой символьной строки, которая пересекается с отрезком [yTop, yBottom],
        // выбираем подходящий блочный символ.
        int firstCharRow = yTop / 2;
        int lastCharRow = yBottom / 2;

        for (int row = firstCharRow; row <= lastCharRow && row < (height + 1) / 2; row++)
        {
            int halfTop = row * 2;        // верхняя "полу-строка" символьной строки
            int halfBottom = row * 2 + 1; // нижняя "полу-строка"

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
                Move(x, row);
                driver.AddRune(glyph);
            }
        }
    }

    private void DrawBars(Rect bounds)
    {
        var driver = Application.Driver;
        int height = bounds.Height;
        int cellWidth = BarWidth + BarGap;
        int numBars = Math.Min(Bars, bounds.Width / Math.Max(1, cellWidth));

        if (numBars <= 0) return;

        float fftStepF = (float)_fft.Length / numBars;

        for (int i = 0; i < numBars; i++)
        {
            int start = (int)(i * fftStepF);
            int end = Math.Max(start + 1, (int)((i + 1) * fftStepF));
            end = Math.Min(end, _fft.Length);

            var rawValue = 0f;
            for (int j = start; j < end; j++)
                rawValue += _fft[j];
            rawValue /= (end - start);

            float k = ((float)Math.Log2(i + 1.3d)) * 10f;
            rawValue *= k;
            rawValue = Math.Clamp(rawValue, 0f, 1f);

            _smoothed[i] = _smoothed[i] * 0.7f + rawValue * 0.3f;
            float barHeight = _smoothed[i] * height;
            int barPixels = Math.Clamp((int)barHeight, 0, height);

            if (barHeight > _peaks[i])
            {
                _peaks[i] = barHeight;
            }
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
                    Move(xStart + dx, height - 1 - y);
                    driver.AddRune('█');
                }
            }

            int peakY = height - 1 - (int)_peaks[i];
            if (peakY >= 0 && peakY < height)
            {
                driver.SetAttribute(new Attribute(Color.White, Color.Black));
                for (int dx = 0; dx < BarWidth; dx++)
                {
                    Move(xStart + dx, peakY);
                    driver.AddRune('░');
                }
            }
        }
    }
}
