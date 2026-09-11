using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TguiColor = Terminal.Gui.Color;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Renders cover images into ASCII art with Floyd–Steinberg dithering.
/// The heavy image processing runs on the thread pool, and results are cached by
/// (path, width, height) so switching tracks or opening several windows does not
/// re-decode the same cover.
/// </summary>
public sealed class CoverArtService : ICoverArtService
{
	private const int MaxCacheEntries = 32;

	private readonly ConcurrentDictionary<CacheKey, Task<CoverArt?>> _cache = new();
	private readonly ConcurrentQueue<CacheKey> _insertionOrder = new();
	private readonly object _trimLock = new();

	public Task<CoverArt?> RenderAsync(string? imagePath, int width, int height, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(imagePath) || width <= 0 || height <= 0)
			return Task.FromResult<CoverArt?>(null);

		var key = new CacheKey(imagePath, width, height);
		bool added = false;

		Task<CoverArt?> task = _cache.GetOrAdd(key, k =>
		{
			added = true;
			return Task.Run(() => Render(k.Path, k.Width, k.Height), CancellationToken.None);
		});

		if (added)
		{
			_insertionOrder.Enqueue(key);
			TrimCache();
		}

		return task;
	}

	private void TrimCache()
	{
		if (_cache.Count <= MaxCacheEntries)
			return;

		lock (_trimLock)
		{
			while (_cache.Count > MaxCacheEntries && _insertionOrder.TryDequeue(out CacheKey oldest))
				_cache.TryRemove(oldest, out _);
		}
	}

	private static CoverArt? Render(string imagePath, int targetWidth, int targetHeight)
	{
		try
		{
			if (!File.Exists(imagePath))
				return null;

			using Image<Rgba32> image = Image.Load<Rgba32>(imagePath);

			// Подготовка: ресайз и легкий пре-процессинг
			image.Mutate(ctx => ctx
				.Resize(targetWidth * 2, targetHeight * 2)
				.Contrast(1.1f));

			int w = image.Width;
			int h = image.Height;
			if (w < 2 || h < 2)
				return null;

			// Создаем рабочую копию в float для накопления ошибки (чтобы не было потерь точности)
			float[,] rErr = new float[w, h];
			float[,] gErr = new float[w, h];
			float[,] bErr = new float[w, h];

			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					Rgba32 p = image[x, y];
					rErr[x, y] = p.R;
					gErr[x, y] = p.G;
					bErr[x, y] = p.B;
				}
			}

			int outW = w / 2;
			int outH = h / 2;
			var pixels = new CoverPixel[outW * outH];

			// Идем по блокам 2x2 (для формирования символов-псевдографики)
			for (int y = 0, oy = 0; y + 1 < h; y += 2, oy++)
			{
				for (int x = 0, ox = 0; x + 1 < w; x += 2, ox++)
				{
					// Берем 4 пикселя блока, учитывая накопленную ошибку
					Rgba32 p00 = GetCorrectedPixel(rErr, gErr, bErr, x, y);
					Rgba32 p10 = GetCorrectedPixel(rErr, gErr, bErr, x + 1, y);
					Rgba32 p01 = GetCorrectedPixel(rErr, gErr, bErr, x, y + 1);
					Rgba32 p11 = GetCorrectedPixel(rErr, gErr, bErr, x + 1, y + 1);

					// Формируем ячейку (логика выбора символа и цвета)
					CoverPixel cell = BuildCellWithQuantization(p00, p10, p01, p11, out var error00, out var error10, out var error01, out var error11);
					pixels[oy * outW + ox] = cell;

					// Раскидываем ошибку квантования на соседей по классической схеме 7, 3, 5, 1
					DistributeError(rErr, gErr, bErr, x, y, error00);
					DistributeError(rErr, gErr, bErr, x + 1, y, error10);
					DistributeError(rErr, gErr, bErr, x, y + 1, error01);
					DistributeError(rErr, gErr, bErr, x + 1, y + 1, error11);
				}
			}

			return new CoverArt(outW, outH, pixels);
		}
		catch
		{
			// Нечитаемое/битое изображение — обложка просто не отображается.
			return null;
		}
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
		void AddErr(int nx, int ny, float factor)
		{
			if (nx >= 0 && nx < w && ny >= 0 && ny < h)
			{
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

	private static CoverPixel BuildCellWithQuantization(Rgba32 p00, Rgba32 p10, Rgba32 p01, Rgba32 p11,
		out (float R, float G, float B) e00, out (float R, float G, float B) e10, out (float R, float G, float B) e01, out (float R, float G, float B) e11)
	{
		// 1. Вычисляем среднюю яркость и маску
		int l00 = Luma(p00); int l10 = Luma(p10);
		int l01 = Luma(p01); int l11 = Luma(p11);
		int threshold = (Math.Min(Math.Min(l00, l10), Math.Min(l01, l11)) + Math.Max(Math.Max(l00, l10), Math.Max(l01, l11))) / 2;

		int mask = (l00 >= threshold ? 1 : 0) | (l10 >= threshold ? 2 : 0) | (l01 >= threshold ? 4 : 0) | (l11 >= threshold ? 8 : 0);

		// 2. Определяем цвета FG и BG
		var (fgTarget, bgTarget) = GetMaskedAverages(mask, p00, p10, p01, p11);

		// 3. Квантуем цвета под палитру (дизеринг уже внешний)
		TguiColor fg = NearestAnsiColorBasic(fgTarget.R, fgTarget.G, fgTarget.B, out var fgActual);
		TguiColor bg = NearestAnsiColorBasic(bgTarget.R, bgTarget.G, bgTarget.B, out var bgActual);

		// 4. Считаем ошибку для каждого из 4-х пикселей относительно доставшегося цвета (FG или BG)
		e00 = CalcError(p00, (mask & 1) != 0 ? fgActual : bgActual);
		e10 = CalcError(p10, (mask & 2) != 0 ? fgActual : bgActual);
		e01 = CalcError(p01, (mask & 4) != 0 ? fgActual : bgActual);
		e11 = CalcError(p11, (mask & 8) != 0 ? fgActual : bgActual);

		return new CoverPixel(MaskToGlyph(mask), fg, bg);
	}

	private static (float R, float G, float B) CalcError(Rgba32 original, (byte R, byte G, byte B) actual)
		=> (original.R - actual.R, original.G - actual.G, original.B - actual.B);

	private static TguiColor NearestAnsiColorBasic(byte r, byte g, byte b, out (byte R, byte G, byte B) actual)
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

	private readonly record struct CacheKey(string Path, int Width, int Height);
}
