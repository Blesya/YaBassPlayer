using System.Globalization;
using System.Text;
using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Views;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Окно «Статистика»: вкладки с «портретом слушателя» (когда и что слушали) и
/// счётчиками локальной базы. Всё считается локально, сеть не используется.
/// </summary>
public sealed class DatabaseStatisticsView : Dialog, IDatabaseStatisticsView
{
	private const int LabelWidth = 22;
	private const int ColumnWidth = 33;
	private const int BarWidth = 30;
	private const string Shades = "·░▒▓█";

	private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

	private static readonly string[] WeekdayNames = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

	private readonly Label _overviewPage;
	private readonly Label _ritualsPage;
	private readonly Label _hoursPage;
	private readonly Label _tastePage;
	private readonly Label _recordsPage;

	public DatabaseStatisticsView() : base("Статистика", ScreenWidth(), ScreenHeight())
	{
		_overviewPage = CreatePage();
		_ritualsPage = CreatePage();
		_hoursPage = CreatePage();
		_tastePage = CreatePage();
		_recordsPage = CreatePage();

		var tabs = new TabView
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(2)
		};

		tabs.AddTab(CreateTab("Обзор", _overviewPage), andSelect: true);
		tabs.AddTab(CreateTab("Ритуалы", _ritualsPage), andSelect: false);
		tabs.AddTab(CreateTab("Часы", _hoursPage), andSelect: false);
		tabs.AddTab(CreateTab("Вкус", _tastePage), andSelect: false);
		tabs.AddTab(CreateTab("Рекорды", _recordsPage), andSelect: false);

		var closeButton = new Button("Закрыть [ESC]")
		{
			// Кнопка позиционируется явно: диалог сам не раскладывает её до первого Run,
			// а вкладкам нужно ровно две нижние строки.
			X = Pos.Center(),
			Y = Pos.AnchorEnd(1)
		};
		closeButton.Clicked += Close;

		Add(tabs);
		Add(closeButton);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				Close();
				e.Handled = true;
			}
		};
	}

	public void SetStatistics(DatabaseStatistics stats, ListeningStatistics listening)
	{
		_overviewPage.Text = BuildOverview(stats, listening);
		_ritualsPage.Text = BuildRituals(listening);
		_hoursPage.Text = BuildHours(listening);
		_tastePage.Text = BuildTaste(listening);
		_recordsPage.Text = BuildRecords(listening);
	}

	public void Show()
	{
		Application.Run(this);
	}

	public void Close()
	{
		Application.RequestStop(this);
	}

	// ── Страницы ──────────────────────────────────────────────────────────

	private static string BuildOverview(DatabaseStatistics db, ListeningStatistics stats)
	{
		if (stats.TotalListens == 0)
			return EmptyHint();

		var sb = new StringBuilder();
		AppendLine(sb, "Слушал музыку", FormatListeningTime(stats.TotalListeningMs));
		AppendLine(sb, "Прослушиваний", Num(stats.TotalListens));
		AppendLine(sb, "Уникальных треков", Num(stats.DistinctTracks));
		AppendLine(sb, "Артистов", Num(stats.DistinctArtists));

		if (stats.AverageTrackYear is int year)
		{
			int age = Math.Max(0, DateTime.Now.Year - year);
			AppendLine(sb, "Музыкальный возраст", $"{age} лет (в среднем {year} год)");
		}

		if (stats.Decades.Count > 0)
			AppendLine(sb, "Любимое десятилетие", stats.Decades[0].Name);

		if (stats.PeakHour >= 0)
			AppendLine(sb, "Любимый час", $"{stats.PeakHour:00}:00");

		AppendLine(sb, "Профиль", stats.ListenerProfile);
		AppendLine(sb, "Дней с музыкой",
			$"{Num(stats.ActiveDays)} (в среднем {Num(stats.AverageListensPerActiveDay)} треков в день)");
		AppendLine(sb, "Серия",
			$"{stats.CurrentStreakDays} дней подряд (рекорд — {stats.LongestStreakDays})");

		if (stats.TopArtists.Count > 0)
			AppendLine(sb, "Артист года",
				$"{stats.TopArtists[0].Name} — {Num(stats.TopArtists[0].Count)}");

		if (!string.IsNullOrEmpty(stats.MostRepeatedTrackInDay))
			AppendLine(sb, "Трек-наркотик",
				$"«{Fit(stats.MostRepeatedTrackInDay!, 28)}» — {stats.MostRepeatedTrackInDayCount} раз за день");

		sb.AppendLine();
		AppendLine(sb, "Библиотека",
			$"{Num(db.TracksCount)} треков • кэш {db.CachedFilesSize.ToHumanReadableSize()} " +
			$"• БД {db.DatabaseFileSize.ToHumanReadableSize()}");

		return sb.ToString();
	}

	private static string BuildRituals(ListeningStatistics stats)
	{
		if (stats.TotalListens == 0)
			return EmptyHint();

		var sb = new StringBuilder();
		sb.AppendLine("КОГДА ТЫ СЛУШАЕШЬ          (день недели × час; · тишина, █ пик)");

		var ruler = new StringBuilder(new string(' ', 6));
		for (int hour = 0; hour < 24; hour += 3)
			ruler.Append(hour.ToString("00", CultureInfo.InvariantCulture).PadRight(6));
		sb.AppendLine(ruler.ToString().TrimEnd());

		int max = stats.Heatmap.SelectMany(row => row).DefaultIfEmpty(0).Max();

		for (int day = 0; day < WeekdayNames.Length; day++)
		{
			var row = new StringBuilder(WeekdayNames[day].PadRight(6));
			for (int hour = 0; hour < 24; hour++)
			{
				row.Append(Shade(stats.Heatmap[day][hour], max));
				row.Append(' ');
			}

			sb.AppendLine(row.ToString().TrimEnd());
		}

		sb.AppendLine();
		if (stats.PeakHour >= 0)
			sb.AppendLine($"Пик: {stats.PeakHour:00}:00 — {Num(stats.ListensByHour[stats.PeakHour])} прослушиваний");

		sb.Append(stats.ListenerProfile);

		return sb.ToString();
	}

	private static string BuildHours(ListeningStatistics stats)
	{
		if (stats.TotalListens == 0)
			return EmptyHint();

		var sb = new StringBuilder();
		sb.AppendLine("ПО ЧАСАМ СУТОК          (два часа — одна полоса)");
		int max = stats.ListensByHour.DefaultIfEmpty(0).Max();

		for (int bucket = 0; bucket < 12; bucket++)
		{
			int count = stats.ListensByHour[bucket * 2] + stats.ListensByHour[bucket * 2 + 1];
			int length = max == 0 ? 0 : (int)Math.Round((double)BarWidth * count / max);
			string bar = new string('█', length).PadRight(BarWidth);
			sb.AppendLine($"{bucket * 2:00}–{bucket * 2 + 1:00}  {bar} {Num(count),5}");
		}

		return sb.ToString();
	}

	private static string BuildTaste(ListeningStatistics stats)
	{
		if (stats.TotalListens == 0)
			return EmptyHint();

		var sb = new StringBuilder();
		sb.AppendLine("ТОП АРТИСТОВ".PadRight(ColumnWidth) + "ТОП АЛЬБОМОВ");

		for (int i = 0; i < 5; i++)
		{
			sb.AppendLine(
				RatingRow(stats.TopArtists, i).PadRight(ColumnWidth) +
				RatingRow(stats.TopAlbums, i));
		}

		sb.AppendLine();

		if (stats.TopGenres.Count > 0)
		{
			string genres = string.Join(", ", stats.TopGenres.Select(g => $"{g.Name} — {Num(g.Count)}"));
			sb.AppendLine("Жанры: " + Fit(genres, ColumnWidth * 2));
		}

		if (stats.Decades.Count > 0)
		{
			string decades = string.Join(", ", stats.Decades.Select(d => $"{d.Name} — {Num(d.Count)}"));
			sb.AppendLine("Десятилетия: " + Fit(decades, ColumnWidth * 2 - 6));
		}

		int radio = Percent(stats.RadioListens, stats.TotalListens);
		int local = Percent(stats.LocalListens, stats.TotalListens);
		sb.AppendLine($"Радио {radio}% • свои плейлисты {100 - radio}% • локальная музыка {local}%");

		int favorite = Percent(stats.FavoriteArtistListens, stats.TotalListens);
		sb.AppendLine($"Любимый артист — {favorite}% всех прослушиваний");

		return sb.ToString();
	}

	private static string BuildRecords(ListeningStatistics stats)
	{
		if (stats.TotalListens == 0)
			return EmptyHint();

		var sb = new StringBuilder();

		if (stats.BestDay is DateOnly bestDay)
		{
			AppendLine(sb, "Лучший день",
				$"{bestDay.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} — {Num(stats.BestDayListens)} треков");
		}

		AppendLine(sb, "В среднем за день", $"{Num(stats.AverageListensPerActiveDay)} треков");
		AppendLine(sb, "Дней подряд", $"{stats.CurrentStreakDays} (рекорд — {stats.LongestStreakDays})");
		AppendLine(sb, "Слушал ровно раз", $"{Num(stats.OneTimeTracks)} треков");
		AppendLine(sb, "Новых за 30 дней", Num(stats.NewTracksLast30Days));

		if (!string.IsNullOrEmpty(stats.FirstTrack))
		{
			string date = stats.FirstListenDate is DateTime first
				? $" ({first.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)})"
				: string.Empty;
			AppendLine(sb, "Первый трек", Fit(stats.FirstTrack!, 40) + date);
		}

		if (stats.ForgottenTracks.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("Забытые жемчужины (часто слушал, но давно):");
			foreach (ForgottenTrack track in stats.ForgottenTracks)
			{
				sb.AppendLine(
					$"  • {Fit(track.Name, 35)} — {Num(track.Listens)} раз, " +
					$"последний {track.LastListened.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");
			}
		}

		return sb.ToString();
	}

	// ── Форматирование ────────────────────────────────────────────────────

	private static Label CreatePage() => new()
	{
		X = 1,
		Y = 0,
		Width = Dim.Fill(1),
		Height = Dim.Fill(),
		AutoSize = false,
		Text = string.Empty
	};

	private static TabView.Tab CreateTab(string title, View view) => new()
	{
		Text = title,
		View = view
	};

	private static string EmptyHint()
		=> "Пока нет прослушиваний.\nВключи музыку — и здесь появится твой портрет слушателя.";

	private static void AppendLine(StringBuilder sb, string label, string value)
		=> sb.AppendLine(label.PadRight(LabelWidth) + value);

	private static string RatingRow(IReadOnlyList<StatisticsEntry> entries, int index)
	{
		if (index >= entries.Count)
			return string.Empty;

		StatisticsEntry entry = entries[index];
		return $"{index + 1}. {Fit(entry.Name, 20).PadRight(20)} {Num(entry.Count),5}";
	}

	private static char Shade(int count, int max)
	{
		if (count <= 0 || max <= 0)
			return Shades[0];

		int level = (int)Math.Ceiling(4.0 * count / max);
		return Shades[Math.Clamp(level, 1, 4)];
	}

	private static string Fit(string text, int width)
	{
		if (width <= 0 || string.IsNullOrEmpty(text))
			return string.Empty;

		if (text.Length <= width)
			return text;

		return width == 1 ? "…" : text[..(width - 1)] + "…";
	}

	private static string Num(int value) => Num((long)value);

	private static string Num(long value) => value.ToString("N0", RuCulture).Replace('\u00A0', ' ');

	private static int Percent(int part, int total)
		=> total == 0 ? 0 : (int)Math.Round(100.0 * part / total);

	private static string FormatListeningTime(long milliseconds)
	{
		var span = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));

		if (span.TotalHours >= 1)
			return $"{(int)span.TotalHours} ч {span.Minutes} мин";

		return span.TotalMinutes >= 1 ? $"{span.Minutes} мин" : "меньше минуты";
	}

	private static int ScreenWidth()
	{
		int cols = Application.Driver?.Cols ?? 100;
		return Math.Clamp((int)(cols * 0.92), 60, 120);
	}

	private static int ScreenHeight()
	{
		int rows = Application.Driver?.Rows ?? 40;
		return Math.Clamp((int)(rows * 0.9), 20, 50);
	}
}
