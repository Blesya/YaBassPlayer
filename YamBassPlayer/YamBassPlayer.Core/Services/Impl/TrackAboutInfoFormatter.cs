using System.Text;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Готовит текст окна «О треке». Вынесено из вьюхи, чтобы формат можно было
/// проверить тестами без Terminal.Gui.
/// </summary>
public static class TrackAboutInfoFormatter
{
	public static string Format(TrackAboutInfo info)
	{
		var text = new StringBuilder();

		text.AppendLine($"Исполнитель: {info.Artist}");
		text.AppendLine($"Название: {info.Title}");

		if (!string.IsNullOrWhiteSpace(info.Album))
			text.AppendLine($"Альбом: {info.Album}");

		if (!info.IsAvailable)
		{
			text.AppendLine();
			text.AppendLine("Доступность: Недоступен");

			if (!string.IsNullOrWhiteSpace(info.UnavailableReason))
				text.AppendLine($"Причина: {info.UnavailableReason}");

			if (!string.IsNullOrWhiteSpace(info.SubstituteTitle))
				text.AppendLine($"Играет замена: {info.SubstituteTitle}");
		}

		if (!string.IsNullOrWhiteSpace(info.Note))
		{
			text.AppendLine();
			text.AppendLine($"Примечание: {info.Note}");
		}

		if (info.Labels.Count > 0)
		{
			text.AppendLine();
			text.AppendLine("Метки:");
			foreach (TrackAboutLabel label in info.Labels)
				text.AppendLine($" • {label.Text}");
		}

		if (info.Credits.Count > 0)
		{
			text.AppendLine();
			text.AppendLine("Кредиты:");

			int roleWidth = info.Credits.Max(credit => credit.Title.Length);
			foreach (TrackAboutCredit credit in info.Credits)
				text.AppendLine($" {credit.Title.PadRight(roleWidth)}  {credit.Value}");
		}

		if (!string.IsNullOrWhiteSpace(info.ArtistDescription))
		{
			text.AppendLine();
			text.AppendLine("Об исполнителе:");
			text.AppendLine(info.ArtistDescription);
		}

		return text.ToString().TrimEnd();
	}
}
