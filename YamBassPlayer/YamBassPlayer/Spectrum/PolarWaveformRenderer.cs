using Terminal.Gui;
using YamBassPlayer.Enums;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

public sealed class PolarWaveformRenderer : ISpectrumRenderer
{
    public string DisplayName => "◎ Поляр.";
    public SpectrumDataType DataType => SpectrumDataType.Waveform;

    private float _phase;
    private float _glowIntensity;
    private int _framesSinceLastBeat;

    public void Render(Rect bounds, ConsoleDriver driver, View host, float[] data, int maxFrequencyHz)
    {
        int w = bounds.Width, h = bounds.Height;
        if (w < 4 || h < 4 || data.Length == 0) return;

        float cx = (w - 1) / 2f, cy = (h - 1) / 2f;
        float maxR = Math.Min(cx, cy);
        float baseR = maxR * 0.7f;
        const float xAspect = 2.2f;

        // === Низкочастотная детекция бита: усреднение по группам (LPF) + crest factor ===
        // Усреднение 16 групп по 32 сэмпла убивает высокие частоты (вокал, тарелки).
        // Низкие частоты (бочка, бас) сохраняют форму — crest factor работает по делу.
        const int chunkSize = 32;
        int numChunks = data.Length / chunkSize;  // 16 групп

        // Проход 1: усреднение + поиск пика для ampScale (по сырым данным)
        float rawPeak = 0.0001f;
        float smoothedPeak = 0.0001f;
        float smoothedSumSq = 0f;

        for (int c = 0; c < numChunks; c++)
        {
            float sum = 0f;
            int offset = c * chunkSize;
            for (int i = 0; i < chunkSize; i++)
            {
                float v = data[offset + i];
                sum += v;
                float a = Math.Abs(v);
                if (a > rawPeak) rawPeak = a;
            }
            float avg = sum / chunkSize;
            float absAvg = Math.Abs(avg);
            if (absAvg > smoothedPeak) smoothedPeak = absAvg;
            smoothedSumSq += avg * avg;
        }

        float smoothedRms = MathF.Sqrt(smoothedSumSq / numChunks);
        float crestFactor = smoothedPeak / Math.Max(smoothedRms, 0.001f);

        const float crestThreshold = 2.2f;  // Порог ниже: усреднение сглаживает пики
        const float minPeak = 0.12f;        // Ниже: усреднение снижает амплитуду
        const int beatCooldown = 6;

        if (crestFactor > crestThreshold && smoothedPeak > minPeak && _framesSinceLastBeat >= beatCooldown)
        {
            _glowIntensity = 1.0f;
            _framesSinceLastBeat = 0;
        }
        else
        {
            _glowIntensity *= 0.88f;
            if (_glowIntensity < 0.02f) _glowIntensity = 0f;
            _framesSinceLastBeat++;
        }

        float ampScale = baseR * 0.6f / rawPeak;

        // === Фон за пределами круга: красное/оранжевое свечение при бите ===
        if (_glowIntensity > 0.02f)
        {
            // Выбор цвета свечения в зависимости от интенсивности
            Color glowColor = _glowIntensity > 0.7f ? Color.BrightRed
                : _glowIntensity > 0.3f ? Color.Red
                : Color.Brown;
            var glowAttr = new Attribute(Color.Black, glowColor);
            var blackAttr = new Attribute(Color.Black, Color.Black);

            for (int y = 0; y < h; y++)
            {
                float dy = y - cy;
                float absDy = Math.Abs(dy);
                if (absDy <= baseR)
                {
                    float halfWidth = (float)(xAspect * Math.Sqrt(baseR * baseR - dy * dy));
                    int xLeft = Math.Max(0, (int)(cx - halfWidth + 0.5f));
                    int xRight = Math.Min(w - 1, (int)(cx + halfWidth + 0.5f));

                    // Левая внешняя область
                    if (xLeft > 0)
                    {
                        driver.SetAttribute(glowAttr);
                        host.Move(0, y);
                        for (int x = 0; x < xLeft; x++) driver.AddRune(' ');
                    }
                    // Внутренняя область (чёрный фон)
                    driver.SetAttribute(blackAttr);
                    host.Move(xLeft, y);
                    for (int x = xLeft; x <= xRight; x++) driver.AddRune(' ');
                    // Правая внешняя область
                    if (xRight < w - 1)
                    {
                        driver.SetAttribute(glowAttr);
                        host.Move(xRight + 1, y);
                        for (int x = xRight + 1; x < w; x++) driver.AddRune(' ');
                    }
                }
                else
                {
                    // Вся строка за пределами круга
                    driver.SetAttribute(glowAttr);
                    host.Move(0, y);
                    for (int x = 0; x < w; x++) driver.AddRune(' ');
                }
            }
        }

        driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
        for (int a = 0; a < 360; a += 6)
        {
            double rad = a * Math.PI / 180.0;
            int px = (int)(cx + Math.Cos(rad) * baseR * xAspect + 0.5);
            int py = (int)(cy + Math.Sin(rad) * baseR + 0.5);
            if (px >= 0 && px < w && py >= 0 && py < h)
            {
                host.Move(px, py);
                driver.AddRune('·');
            }
        }

        int n = data.Length;
        for (int i = 0; i < n; i++)
        {
            double angle = i / (double)n * Math.PI * 2 + _phase;
            float r = baseR + data[i] * ampScale;
            r = Math.Clamp(r, 0f, maxR * 0.95f);

            int px = (int)(cx + Math.Cos(angle) * r * xAspect + 0.5);
            int py = (int)(cy + Math.Sin(angle) * r + 0.5);
            if (px < 0 || px >= w || py < 0 || py >= h) continue;

            float amp = Math.Abs(data[i]);
            Color c = amp switch
            {
                < 0.08f => Color.BrightCyan,
                < 0.20f => Color.Cyan,
                < 0.40f => Color.Green,
                < 0.60f => Color.BrightGreen,
                < 0.80f => Color.BrightYellow,
                < 0.95f => Color.BrightRed,
                _ => Color.Red
            };
            driver.SetAttribute(new Attribute(c, Color.Black));
            host.Move(px, py);
            driver.AddRune('█');
        }

        int cxi = (int)cx, cyi = (int)cy;
        if (cxi >= 0 && cxi < w && cyi >= 0 && cyi < h)
        {
            driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));
            host.Move(cxi, cyi);
            driver.AddRune('+');
        }

        _phase += 0.02f;
        if (_phase >= Math.PI * 2) _phase -= (float)(Math.PI * 2);
    }

    public void Reset()
    {
        _phase = 0;
        _glowIntensity = 0;
        _framesSinceLastBeat = 0;
    }
}
