using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui;
using TguiColor = Terminal.Gui.Color;
using TguiAttribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Views.Impl;

internal sealed class CoverAsciiView : View
{
    private (char Ch, TguiColor Fg, TguiColor Bg)[]? _pixelBuffer;
    private int _bufWidth;
    private int _bufHeight;

    // Добавляем перегрузку для совместимости со старым кодом
    public void SetPixels(List<List<(char Ch, TguiColor Fg, TguiColor Bg)>>? pixels)
    {
        if (pixels == null || pixels.Count == 0)
        {
            _pixelBuffer = null;
            return;
        }

        _bufHeight = pixels.Count;
        _bufWidth = pixels[0].Count;
        _pixelBuffer = new (char Ch, TguiColor Fg, TguiColor Bg)[_bufWidth * _bufHeight];

        for (int y = 0; y < _bufHeight; y++)
            for (int x = 0; x < _bufWidth; x++)
                _pixelBuffer[y * _bufWidth + x] = pixels[y][x];

        SetNeedsDisplay();
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        if (_pixelBuffer is null) return;

        for (int row = 0; row < _bufHeight && row < bounds.Height; row++)
        {
            for (int col = 0; col < _bufWidth && col < bounds.Width; col++)
            {
                var (ch, fg, bg) = _pixelBuffer[row * _bufWidth + col];
                Driver.SetAttribute(new TguiAttribute(fg, bg));

                // Фикс ошибки CS1503: явно используем System.Text.Rune
                AddRune(col, row, (uint)ch);
            }
        }
    }

    public static List<List<(char Ch, TguiColor Fg, TguiColor Bg)>> RenderAscii(string imagePath, int targetWidth, int targetHeight)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(imagePath);
        
        // Подготовка: ресайз и легкий пре-процессинг
        image.Mutate(ctx => ctx
            .Resize(targetWidth * 2, targetHeight * 2)
            .Contrast(1.1f));

        int w = image.Width;
        int h = image.Height;

        // Создаем рабочую копию в float для накопления ошибки (чтобы не было потерь точности)
        float[,] rErr = new float[w, h];
        float[,] gErr = new float[w, h];
        float[,] bErr = new float[w, h];

        // Инициализируем массив изначальными значениями
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                rErr[x, y] = image[x, y].R;
                gErr[x, y] = image[x, y].G;
                bErr[x, y] = image[x, y].B;
            }
        }

        var rows = new List<List<(char Ch, TguiColor Fg, TguiColor Bg)>>();

        // Идем по блокам 2x2 (для формирования символов-псевдографики)
        for (int y = 0; y + 1 < h; y += 2)
        {
            var row = new List<(char Ch, TguiColor Fg, TguiColor Bg)>();

            for (int x = 0; x + 1 < w; x += 2)
            {
                // Берем 4 пикселя блока, учитывая накопленную ошибку
                Rgba32 p00 = GetCorrectedPixel(rErr, gErr, bErr, x, y);
                Rgba32 p10 = GetCorrectedPixel(rErr, gErr, bErr, x + 1, y);
                Rgba32 p01 = GetCorrectedPixel(rErr, gErr, bErr, x, y + 1);
                Rgba32 p11 = GetCorrectedPixel(rErr, gErr, bErr, x + 1, y + 1);

                // Формируем ячейку (логика выбора символа и цвета)
                var cell = BuildCellWithQuantization(p00, p10, p01, p11, out var error00, out var error10, out var error01, out var error11);
                row.Add(cell);

                // Раскидываем ошибку квантования на соседей по классической схеме 7, 3, 5, 1
                DistributeError(rErr, gErr, bErr, x, y, error00);
                DistributeError(rErr, gErr, bErr, x + 1, y, error10);
                DistributeError(rErr, gErr, bErr, x, y + 1, error01);
                DistributeError(rErr, gErr, bErr, x + 1, y + 1, error11);
            }
            rows.Add(row);
        }

        return rows;
    }

    private static (Rgba32 fg, Rgba32 bg) GetMaskedAverages(int mask, Rgba32 p00, Rgba32 p10, Rgba32 p01, Rgba32 p11)
    {
        int fr = 0, fg = 0, fb = 0, fc = 0;
        int br = 0, bg = 0, bb = 0, bc = 0;

        void Add(bool isFg, Rgba32 p)
        {
            if (isFg) { fr += p.R; fg += p.G; fb += p.B; fc++; }
            else { br += p.R; bg += p.G; bb += p.B; bc++; }
        }

        Add((mask & 1) != 0, p00); Add((mask & 2) != 0, p10);
        Add((mask & 4) != 0, p01); Add((mask & 8) != 0, p11);

        return (
            fc == 0 ? p00 : new Rgba32((byte)(fr / fc), (byte)(fg / fc), (byte)(fb / fc)),
            bc == 0 ? p00 : new Rgba32((byte)(br / bc), (byte)(bg / bc), (byte)(bb / bc))
        );
    }

    private static char MaskToGlyph(int mask) => mask switch
    {
        0b0000 => ' ',
        0b1111 => '█',
        0b0001 => '▘',
        0b0010 => '▝',
        0b0100 => '▖',
        0b1000 => '▗',
        0b0011 => '▀',
        0b1100 => '▄',
        0b0101 => '▌',
        0b1010 => '▐',
        0b1001 => '▚',
        0b0110 => '▞',
        0b0111 => '▛',
        0b1011 => '▜',
        0b1101 => '▙',
        0b1110 => '▟',
        _ => '█'
    };

    private static int Luma(Rgba32 c) => (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);

    private static Rgba32 GetCorrectedPixel(float[,] rE, float[,] gE, float[,] bE, int x, int y)
    {
        return new Rgba32(
            (byte)Math.Clamp(rE[x, y], 0, 255),
            (byte)Math.Clamp(gE[x, y], 0, 255),
            (byte)Math.Clamp(bE[x, y], 0, 255)
        );
    }

    private static void DistributeError(float[,] rE, float[,] gE, float[,] bE, int x, int y, (float R, float G, float B) err)
    {
        int w = rE.GetLength(0);
        int h = rE.GetLength(1);

        // Floyd-Steinberg коэффициенты: 7/16 вправо, 3/16 вниз-влево, 5/16 вниз, 1/16 вниз-вправо
        void AddErr(int nx, int ny, float factor) {
            if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
                rE[nx, ny] += err.R * factor;
                gE[nx, ny] += err.G * factor;
                bE[nx, ny] += err.B * factor;
            }
        }

        AddErr(x + 1, y, 7f / 16f);
        AddErr(x - 1, y + 1, 3f / 16f);
        AddErr(x, y + 1, 5f / 16f);
        AddErr(x + 1, y + 1, 1f / 16f);
    }

    private static (char Ch, TguiColor Fg, TguiColor Bg) BuildCellWithQuantization(Rgba32 p00, Rgba32 p10, Rgba32 p01, Rgba32 p11, 
        out (float R, float G, float B) e00, out (float R, float G, float B) e10, out (float R, float G, float B) e01, out (float R, float G, float B) e11)
    {
        // 1. Вычисляем среднюю яркость и маску как раньше
        int l00 = Luma(p00); int l10 = Luma(p10);
        int l01 = Luma(p01); int l11 = Luma(p11);
        int threshold = (Math.Min(Math.Min(l00, l10), Math.Min(l01, l11)) + Math.Max(Math.Max(l00, l10), Math.Max(l01, l11))) / 2;

        int mask = (l00 >= threshold ? 1 : 0) | (l10 >= threshold ? 2 : 0) | (l01 >= threshold ? 4 : 0) | (l11 >= threshold ? 8 : 0);
        
        // 2. Определяем цвета FG и BG
        var (fgTarget, bgTarget) = GetMaskedAverages(mask, p00, p10, p01, p11);
        
        // 3. Квантуем цвета под палитру (используем NearestAnsiColorBasic без шахматки, так как дизеринг теперь внешний)
        TguiColor fg = NearestAnsiColorBasic(fgTarget.R, fgTarget.G, fgTarget.B, out var fgActual);
        TguiColor bg = NearestAnsiColorBasic(bgTarget.R, bgTarget.G, bgTarget.B, out var bgActual);

        // 4. Считаем ошибку для каждого из 4-х пикселей относительно того, какой цвет им достался (FG или BG)
        e00 = CalcError(p00, (mask & 1) != 0 ? fgActual : bgActual);
        e10 = CalcError(p10, (mask & 2) != 0 ? fgActual : bgActual);
        e01 = CalcError(p01, (mask & 4) != 0 ? fgActual : bgActual);
        e11 = CalcError(p11, (mask & 8) != 0 ? fgActual : bgActual);

        return (MaskToGlyph(mask), fg, bg);
    }

    private static (float R, float G, float B) CalcError(Rgba32 original, (byte R, byte G, byte B) actual) 
        => (original.R - actual.R, original.G - actual.G, original.B - actual.B);

    public static TguiColor NearestAnsiColorBasic(byte r, byte g, byte b, out (byte R, byte G, byte B) actual)
    {
        ReadOnlySpan<(TguiColor Col, byte R, byte G, byte B)> palette = [
            (TguiColor.Black, 0, 0, 0), (TguiColor.Blue, 0, 0, 180),
            (TguiColor.Green, 0, 180, 0), (TguiColor.Cyan, 0, 180, 180),
            (TguiColor.Red, 180, 0, 0), (TguiColor.Magenta, 180, 0, 180),
            (TguiColor.Brown, 150, 75, 0), (TguiColor.Gray, 190, 190, 190),
            (TguiColor.DarkGray, 100, 100, 100), (TguiColor.BrightBlue, 80, 120, 255),
            (TguiColor.BrightGreen, 0, 255, 0), (TguiColor.BrightCyan, 0, 255, 255),
            (TguiColor.BrightRed, 255, 80, 80), (TguiColor.BrightMagenta, 255, 0, 255),
            (TguiColor.BrightYellow, 255, 255, 0), (TguiColor.White, 255, 255, 255)
        ];

        int bestDist = int.MaxValue;
        int bestIdx = 0;

        for (int i = 0; i < palette.Length; i++)
        {
            var p = palette[i];
            int dr = r - p.R; int dg = g - p.G; int db = b - p.B;
            int d = (2 * dr * dr) + (4 * dg * dg) + (3 * db * db);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }

        actual = (palette[bestIdx].R, palette[bestIdx].G, palette[bestIdx].B);
        return palette[bestIdx].Col;
    }
}