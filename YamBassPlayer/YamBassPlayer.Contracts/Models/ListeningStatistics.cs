namespace YamBassPlayer.Models;

/// <summary>
/// «Портрет слушателя», посчитанный из истории прослушиваний. Все поля выводятся
/// из <c>listensHistory</c> и метаданных <c>Tracks</c>, поэтому схема БД не меняется.
/// Времена суток и дни недели — локальные: смещение берётся из каждой записи истории.
/// </summary>
public sealed class ListeningStatistics
{
	/// <summary>Сколько раз запускали треки (запись появляется после 30 секунд игры).</summary>
	public int TotalListens { get; init; }

	/// <summary>Суммарная длительность прослушанных треков; оценка, а не секундомер.</summary>
	public long TotalListeningMs { get; init; }

	public int DistinctTracks { get; init; }
	public int DistinctArtists { get; init; }

	/// <summary>Средний год выпуска прослушанных треков; null — метаданных о годах нет.</summary>
	public int? AverageTrackYear { get; init; }

	/// <summary>Часы выпуска — по убыванию прослушиваний.</summary>
	public IReadOnlyList<StatisticsEntry> Decades { get; init; } = [];

	/// <summary>Прослушивания по локальным часам суток, индекс 0–23.</summary>
	public int[] ListensByHour { get; init; } = new int[24];

	/// <summary>Тепловая карта «день недели × час», дни с понедельника, часы 0–23.</summary>
	public int[][] Heatmap { get; init; } = CreateEmptyHeatmap();

	/// <summary>Самый частый час суток; -1 — прослушиваний нет.</summary>
	public int PeakHour { get; init; } = -1;

	/// <summary>Готовая подпись профиля: «Сова», «Жаворонок», «Вечерний меломан».</summary>
	public string ListenerProfile { get; init; } = string.Empty;

	public IReadOnlyList<StatisticsEntry> TopArtists { get; init; } = [];
	public IReadOnlyList<StatisticsEntry> TopAlbums { get; init; } = [];
	public IReadOnlyList<StatisticsEntry> TopGenres { get; init; } = [];

	/// <summary>Прослушивания самого частого артиста — для доли «одержимости».</summary>
	public int FavoriteArtistListens { get; init; }

	public int RadioListens { get; init; }
	public int LocalListens { get; init; }

	public int ActiveDays { get; init; }
	public int AverageListensPerActiveDay { get; init; }

	public DateOnly? BestDay { get; init; }
	public int BestDayListens { get; init; }

	public int CurrentStreakDays { get; init; }
	public int LongestStreakDays { get; init; }

	/// <summary>Трек с максимумом повторов за один день, например «Кино — Группа крови».</summary>
	public string? MostRepeatedTrackInDay { get; init; }

	public int MostRepeatedTrackInDayCount { get; init; }

	public IReadOnlyList<ForgottenTrack> ForgottenTracks { get; init; } = [];

	/// <summary>Сколько треков слушали ровно один раз.</summary>
	public int OneTimeTracks { get; init; }

	public string? FirstTrack { get; init; }
	public DateTime? FirstListenDate { get; init; }

	/// <summary>Сколько треков впервые услышаны за последние 30 дней.</summary>
	public int NewTracksLast30Days { get; init; }

	public static int[][] CreateEmptyHeatmap()
	{
		var map = new int[7][];
		for (int day = 0; day < map.Length; day++)
			map[day] = new int[24];

		return map;
	}
}
