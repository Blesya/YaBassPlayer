using Terminal.Gui;
using YamBassPlayer.Services;
using TguiAttribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Displays pre-rendered ASCII cover art. Image decoding/dithering lives in
/// <see cref="ICoverArtService"/>; this view only blits the finished pixel buffer.
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

        for (int row = 0; row < height && row < bounds.Height; row++)
        {
            for (int col = 0; col < width && col < bounds.Width; col++)
            {
                CoverPixel pixel = pixels[row * width + col];
                Driver.SetAttribute(new TguiAttribute(pixel.Fg, pixel.Bg));

                // Фикс ошибки CS1503: явно используем System.Text.Rune
                AddRune(col, row, (uint)pixel.Ch);
            }
        }
    }
}
