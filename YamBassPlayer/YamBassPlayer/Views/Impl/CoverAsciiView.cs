using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui;
using TguiColor = Terminal.Gui.Color;
using TguiAttribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Views.Impl;

internal sealed class CoverAsciiView : View
{
    private List<List<(char Ch, TguiColor Fg, TguiColor Bg)>>? _pixels;

    public void SetPixels(List<List<(char Ch, TguiColor Fg, TguiColor Bg)>>? pixels)
    {
        _pixels = pixels;
        SetNeedsDisplay();
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        if (_pixels is null) return;

        for (int row = 0; row < _pixels.Count && row < bounds.Height; row++)
        {
            var line = _pixels[row];
            for (int col = 0; col < line.Count && col < bounds.Width; col++)
            {
                (char ch, TguiColor fg, TguiColor bg) = line[col];
                Driver.SetAttribute(new TguiAttribute(fg, bg));
                AddRune(col, row, ch);
            }
        }
    }

    public static List<List<(char Ch, TguiColor Fg, TguiColor Bg)>> RenderAscii(string imagePath, int targetWidth, int targetHeight)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(imagePath);

        int pixelWidth = Math.Max(16, Math.Min(targetWidth * 2, 1280));
        int pixelHeight = Math.Max(16, Math.Min(targetHeight * 2, 480));

        ResizeToFit(image, pixelWidth, pixelHeight);

        image.Mutate(ctx => ctx
            .Contrast(1.15f)
            .Brightness(1.05f));

        var rows = new List<List<(char Ch, TguiColor Fg, TguiColor Bg)>>();

        for (int y = 0; y + 1 < image.Height; y += 2)
        {
            var row = new List<(char Ch, TguiColor Fg, TguiColor Bg)>();

            for (int x = 0; x + 1 < image.Width; x += 2)
            {
                row.Add(BuildCell(
                    image[x, y],
                    image[x + 1, y],
                    image[x, y + 1],
                    image[x + 1, y + 1]));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static void ResizeToFit(Image<Rgba32> image, int targetWidth, int targetHeight)
    {
        int newWidth = targetWidth % 2 == 0 ? targetWidth : targetWidth - 1;
        int newHeight = targetHeight % 2 == 0 ? targetHeight : targetHeight - 1;

        newWidth = Math.Max(2, newWidth);
        newHeight = Math.Max(2, newHeight);

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(newWidth, newHeight),
            Mode = ResizeMode.Stretch
        }));
    }

    private static (char Ch, TguiColor Fg, TguiColor Bg) BuildCell(Rgba32 p00, Rgba32 p10, Rgba32 p01, Rgba32 p11)
    {
        int l00 = Luma(p00);
        int l10 = Luma(p10);
        int l01 = Luma(p01);
        int l11 = Luma(p11);

        int min = Math.Min(Math.Min(l00, l10), Math.Min(l01, l11));
        int max = Math.Max(Math.Max(l00, l10), Math.Max(l01, l11));

        // Если блок почти однотонный — не выдумываем форму
        if (max - min < 24)
        {
            var avg = Average(p00, p10, p01, p11);
            return ('█', NearestAnsiColor(avg.R, avg.G, avg.B), NearestAnsiColor(avg.R, avg.G, avg.B));
        }

        int threshold = (min + max) / 2;

        bool b00 = l00 >= threshold; // top-left
        bool b10 = l10 >= threshold; // top-right
        bool b01 = l01 >= threshold; // bottom-left
        bool b11 = l11 >= threshold; // bottom-right

        int mask =
            (b00 ? 1 : 0) |
            (b10 ? 2 : 0) |
            (b01 ? 4 : 0) |
            (b11 ? 8 : 0);

        char ch = MaskToGlyph(mask);

        var fgPixels = new List<Rgba32>(4);
        var bgPixels = new List<Rgba32>(4);

        AddByMask(b00, p00, fgPixels, bgPixels);
        AddByMask(b10, p10, fgPixels, bgPixels);
        AddByMask(b01, p01, fgPixels, bgPixels);
        AddByMask(b11, p11, fgPixels, bgPixels);

        if (fgPixels.Count == 0)
        {
            var avg = Average(p00, p10, p01, p11);
            var c = NearestAnsiColor(avg.R, avg.G, avg.B);
            return (' ', c, c);
        }

        if (bgPixels.Count == 0)
        {
            var avg = Average(p00, p10, p01, p11);
            var c = NearestAnsiColor(avg.R, avg.G, avg.B);
            return ('█', c, c);
        }

        Rgba32 fgAvg = Average(fgPixels);
        Rgba32 bgAvg = Average(bgPixels);

        TguiColor fg = NearestAnsiColor(fgAvg.R, fgAvg.G, fgAvg.B);
        TguiColor bg = NearestAnsiColor(bgAvg.R, bgAvg.G, bgAvg.B);

        return (ch, fg, bg);
    }

    private static char MaskToGlyph(int mask) => mask switch
    {
        0b0000 => ' ',
        0b1111 => '█',

        0b0001 => '▘', // top-left
        0b0010 => '▝', // top-right
        0b0100 => '▖', // bottom-left
        0b1000 => '▗', // bottom-right

        0b0011 => '▀', // top half
        0b1100 => '▄', // bottom half
        0b0101 => '▌', // left half
        0b1010 => '▐', // right half

        0b1001 => '▚', // tl + br
        0b0110 => '▞', // tr + bl

        0b0111 => '▛', // all except br
        0b1011 => '▜', // all except bl
        0b1101 => '▙', // all except tr
        0b1110 => '▟', // all except tl

        _ => '█'
    };

    private static void AddByMask(bool isFg, Rgba32 pixel, List<Rgba32> fgPixels, List<Rgba32> bgPixels)
    {
        if (isFg)
            fgPixels.Add(pixel);
        else
            bgPixels.Add(pixel);
    }

    private static int Luma(Rgba32 c) => (299 * c.R + 587 * c.G + 114 * c.B) / 1000;

    private static Rgba32 Average(params Rgba32[] pixels) => Average((IReadOnlyList<Rgba32>)pixels);

    private static Rgba32 Average(IReadOnlyList<Rgba32> pixels)
    {
        if (pixels.Count == 0)
            return new Rgba32(0, 0, 0);

        int r = 0, g = 0, b = 0, a = 0;
        for (int i = 0; i < pixels.Count; i++)
        {
            r += pixels[i].R;
            g += pixels[i].G;
            b += pixels[i].B;
            a += pixels[i].A;
        }

        return new Rgba32(
            (byte)(r / pixels.Count),
            (byte)(g / pixels.Count),
            (byte)(b / pixels.Count),
            (byte)(a / pixels.Count));
    }

    public static TguiColor NearestAnsiColor(byte r, byte g, byte b)
    {
        ReadOnlySpan<(TguiColor Col, int R, int G, int B)> palette =
        [
            (TguiColor.Black,           0,   0,   0),
            (TguiColor.Blue,            0,   0, 200),
            (TguiColor.Green,           0, 200,   0),
            (TguiColor.Cyan,            0, 200, 200),
            (TguiColor.Red,           200,   0,   0),
            (TguiColor.Magenta,       200,   0, 200),
            (TguiColor.Gray,          192, 192, 192),
            (TguiColor.DarkGray,      128, 128, 128),
            (TguiColor.BrightBlue,     80, 120, 255),
            (TguiColor.BrightGreen,     0, 255,   0),
            (TguiColor.BrightCyan,      0, 255, 255),
            (TguiColor.BrightRed,     255,  80,  80),
            (TguiColor.BrightMagenta, 255,   0, 255),
            (TguiColor.BrightYellow,  255, 255,   0),
            (TguiColor.White,         255, 255, 255),
        ];

        TguiColor best = TguiColor.White;
        int bestDist = int.MaxValue;
        foreach ((TguiColor col, int pr, int pg, int pb) in palette)
        {
            int dist = (r - pr) * (r - pr) + (g - pg) * (g - pg) + (b - pb) * (b - pb);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = col;
            }
        }

        return best;
    }
}
