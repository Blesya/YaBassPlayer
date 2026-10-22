using System.Globalization;

namespace YamBassPlayer.Extensions;

public static class FileSizeExtensions
{
	private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ"];

	// Интерфейс русский: разделитель дробной части — всегда запятая, независимо от локали машины.
	private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

	public static string ToHumanReadableSize(this long bytes)
	{
		if (bytes == 0)
			return "0 Б";

		double size = bytes;
		int unitIndex = 0;

		while (size >= 1024 && unitIndex < Units.Length - 1)
		{
			size /= 1024;
			unitIndex++;
		}

		return size % 1 == 0
			? $"{size.ToString("F0", Culture)} {Units[unitIndex]}"
			: $"{size.ToString("F1", Culture)} {Units[unitIndex]}";
	}
}
