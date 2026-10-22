using System.Globalization;
using YamBassPlayer.Models;

namespace YamBassPlayer.Extensions;

public static class TrackDownloadStateExtensions
{
	/// <summary>
	/// Короткая подпись прогресса для узкой колонки списка: «45%», а если сервер
	/// не сообщил полный размер — уже полученный объём («512K», «1.2M»).
	/// </summary>
	public static string ToShortProgress(this TrackDownloadState state)
		=> state.HasTotal
			? state.Percent.ToString(CultureInfo.InvariantCulture) + "%"
			: ToCompactSize(state.BytesReceived);

	private static string ToCompactSize(long bytes)
	{
		if (bytes < 1024)
			return bytes.ToString(CultureInfo.InvariantCulture) + "B";

		if (bytes < 1024 * 1024)
			return (bytes / 1024).ToString(CultureInfo.InvariantCulture) + "K";

		return (bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture) + "M";
	}
}
