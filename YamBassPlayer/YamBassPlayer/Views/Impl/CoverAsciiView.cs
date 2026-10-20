using Terminal.Gui;
using YamBassPlayer.Services;
using YamBassPlayer.TerminalOutput;
using TguiAttribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Displays pre-rendered ASCII cover art. Image decoding/dithering lives in
/// <see cref="ICoverArtService"/>; this view only blits the finished pixel buffer.
/// На TrueColor-драйвере клетки рисуются 24-битным цветом, иначе — 16-цветным.
/// </summary>
internal sealed class CoverAsciiView : View
{
    private CoverArt? _art;

    public void SetPixels(CoverArt? art)
    {
        _art = art;
        SetNeedsDisplay();
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        if (_art is null) return;

        int width = _art.Width;
        int height = _art.Height;
        CoverPixel[] pixels = _art.Pixels;
        bool trueColor = _art.TrueColor && Driver is ITrueColorDriver;
        var rgbDriver = trueColor ? (ITrueColorDriver)Driver : null;

        for (int row = 0; row < height && row < bounds.Height; row++)
        {
            for (int col = 0; col < width && col < bounds.Width; col++)
            {
                CoverPixel pixel = pixels[row * width + col];

                if (rgbDriver is not null)
                {
                    rgbDriver.SetRgbAttribute(
                        pixel.Fg.R, pixel.Fg.G, pixel.Fg.B,
                        pixel.Bg.R, pixel.Bg.G, pixel.Bg.B);
                }
                else
                {
                    Driver.SetAttribute(new TguiAttribute(pixel.FgAnsi, pixel.BgAnsi));
                }

                // Фикс ошибки CS1503: явно используем System.Text.Rune
                AddRune(col, row, (uint)pixel.Ch);
            }
        }

        // Следующие представления не должны унаследовать последний RGB-цвет обложки.
        rgbDriver?.ResetRgbAttribute();
    }
}
