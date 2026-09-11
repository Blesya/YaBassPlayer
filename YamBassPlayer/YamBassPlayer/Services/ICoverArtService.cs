using Terminal.Gui;

namespace YamBassPlayer.Services;

/// <summary>
/// One terminal cell of rendered cover art: a glyph plus its foreground/background colors.
/// </summary>
public readonly record struct CoverPixel(char Ch, Color Fg, Color Bg);

/// <summary>
/// Immutable snapshot of rendered ASCII cover art (flat row-major pixel grid).
/// </summary>
public sealed record CoverArt(int Width, int Height, CoverPixel[] Pixels);

/// <summary>
/// Renders album-cover images into ASCII art for the terminal.
/// Implementations must run the CPU-heavy image processing off the caller's thread
/// (views invoke this from the UI loop) and cache results by (path, width, height).
/// </summary>
public interface ICoverArtService
{
	/// <summary>
	/// Renders <paramref name="imagePath"/> into ASCII art of the given size, reusing a cached
	/// result when available. Returns <see langword="null"/> when the path is empty/missing or the
	/// image cannot be decoded.
	/// </summary>
	Task<CoverArt?> RenderAsync(string? imagePath, int width, int height, CancellationToken ct = default);
}
