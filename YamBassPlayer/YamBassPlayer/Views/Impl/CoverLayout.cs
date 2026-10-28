namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Раскладка ASCII-обложки в полноэкранном окне: картинка занимает доступную
/// площадь слева, текст — колонку справа. Размер считается от фактического
/// размера окна, поэтому обложка не обрезается на маленьком терминале
/// и растёт на большом.
/// </summary>
internal static class CoverLayout
{
	/// <summary>Пропорции клеточной обложки: 100×55, как в исходном макете.</summary>
	private const int AspectWidth = 20;
	private const int AspectHeight = 11;

	private const int MinCoverWidth = 8;
	private const int MinCoverHeight = 4;
	private const int MinTextWidth = 12;
	private const int MaxTextWidth = 60;
	private const int Gap = 3;

	public static (int Width, int Height) Compute(int availableWidth, int availableHeight)
	{
		int width = Math.Max(1, availableWidth);
		int height = Math.Max(1, availableHeight);

		int textWidth = Math.Clamp(width / 3, MinTextWidth, MaxTextWidth);
		int maxCoverWidth = Math.Max(MinCoverWidth, width - textWidth - Gap);

		int coverHeight = Math.Min(height, maxCoverWidth * AspectHeight / AspectWidth);
		coverHeight = Math.Max(MinCoverHeight, coverHeight);

		int coverWidth = Math.Min(maxCoverWidth, coverHeight * AspectWidth / AspectHeight);
		coverWidth = Math.Max(MinCoverWidth, coverWidth);

		return (coverWidth, coverHeight);
	}
}
