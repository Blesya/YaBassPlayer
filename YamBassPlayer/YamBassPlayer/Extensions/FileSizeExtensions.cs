namespace YamBassPlayer.Extensions;

public static class FileSizeExtensions
{
	private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ"];

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
			? $"{size:F0} {Units[unitIndex]}"
			: $"{size:F1} {Units[unitIndex]}";
	}
}
