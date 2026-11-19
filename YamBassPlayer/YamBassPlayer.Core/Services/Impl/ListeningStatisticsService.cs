using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Считает «портрет слушателя» из истории: время суток и дни недели, серии дней,
/// артистов, альбомы, жанры и курьёзы. Только читает <c>listensHistory</c> и
/// метаданные <c>Tracks</c>, схему БД не меняет.
/// </summary>
public sealed class ListeningStatisticsService : IListeningStatisticsService
{
	private const int TopLimit = 5;
	private const int ForgottenLimit = 3;
	private const int ForgottenMinListens = 3;
	private const int ForgottenQuietDays = 90;
	private const int NewTracksWindowDays = 30;
	private const int HeatmapDays = 7;
	private const int HoursInDay = 24;

	private readonly IDbConnectionFactory _connectionFactory;

	public ListeningStatisticsService(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public ListeningStatistics Collect()
	{
		try
		{
			using var connection = _connectionFactory.Create();
			return CollectCore(connection);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Не удалось собрать статистику прослушиваний");
			return new ListeningStatistics();
		}
	}

	private static ListeningStatistics CollectCore(SqliteConnection connection)
	{
		int[] byHour = ReadHourHistogram(connection);
		int[][] heatmap = ReadHeatmap(connection);
		IReadOnlyList<(DateOnly Day, int Count)> days = ReadDayCounts(connection);
		(int totalListens, long totalMs, int distinctTracks, int distinctArtists) = ReadTotals(connection);
		IReadOnlyList<StatisticsEntry> artists = ReadTopColumn(connection, "Artist", TopLimit);
		IReadOnlyList<StatisticsEntry> albums = ReadTopColumn(connection, "Album", TopLimit);
		IReadOnlyList<StatisticsEntry> genres = ReadTopGenres(connection, TopLimit);
		IReadOnlyList<StatisticsEntry> decades = ReadDecades(connection, TopLimit);
		(int radioListens, int localListens) = ReadShares(connection);
		int? averageYear = ReadAverageTrackYear(connection);
		(string? repeatedTrack, int repeatedCount) = ReadMostRepeatedInDay(connection);
		IReadOnlyList<ForgottenTrack> forgotten = ReadForgottenTracks(connection);
		int oneTimeTracks = ReadOneTimeTracks(connection);
		(string? firstTrack, DateTime? firstListenDate) = ReadFirstListen(connection);
		int newTracks = ReadNewTracks(connection, NewTracksWindowDays);

		(int currentStreak, int longestStreak) = ComputeStreaks(days);
		(DateOnly? bestDay, int bestDayListens) = FindBestDay(days);

		return new ListeningStatistics
		{
			TotalListens = totalListens,
			TotalListeningMs = totalMs,
			DistinctTracks = distinctTracks,
			DistinctArtists = distinctArtists,
			AverageTrackYear = averageYear,
			Decades = decades,
			ListensByHour = byHour,
			Heatmap = heatmap,
			PeakHour = FindPeakHour(byHour),
			ListenerProfile = BuildListenerProfile(byHour, totalListens),
			TopArtists = artists,
			TopAlbums = albums,
			TopGenres = genres,
			FavoriteArtistListens = artists.Count > 0 ? artists[0].Count : 0,
			RadioListens = radioListens,
			LocalListens = localListens,
			ActiveDays = days.Count,
			AverageListensPerActiveDay = days.Count == 0
				? 0
				: (int)Math.Round((double)totalListens / days.Count),
			BestDay = bestDay,
			BestDayListens = bestDayListens,
			CurrentStreakDays = currentStreak,
			LongestStreakDays = longestStreak,
			MostRepeatedTrackInDay = repeatedTrack,
			MostRepeatedTrackInDayCount = repeatedCount,
			ForgottenTracks = forgotten,
			OneTimeTracks = oneTimeTracks,
			FirstTrack = firstTrack,
			FirstListenDate = firstListenDate,
			NewTracksLast30Days = newTracks
		};
	}

	// ── Запросы ───────────────────────────────────────────────────────────

	private static int[] ReadHourHistogram(SqliteConnection connection)
	{
		var byHour = new int[HoursInDay];

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) AS hour,
			       COUNT(*)
			FROM listensHistory
			GROUP BY hour;
			""";

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			int hour = reader.GetInt32(0);
			if (hour is >= 0 and < HoursInDay)
				byHour[hour] = Convert.ToInt32(reader.GetInt64(1));
		}

		return byHour;
	}

	private static int[][] ReadHeatmap(SqliteConnection connection)
	{
		int[][] heatmap = ListeningStatistics.CreateEmptyHeatmap();

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT CAST(strftime('%w', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) AS weekday,
			       CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) AS hour,
			       COUNT(*)
			FROM listensHistory
			GROUP BY weekday, hour;
			""";

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			int weekday = reader.GetInt32(0);
			int hour = reader.GetInt32(1);
			if (hour is < 0 or >= HoursInDay)
				continue;

			// SQLite: 0 — воскресенье, 6 — суббота. Приводим к понедельнику-первому.
			int dayIndex = (weekday + 6) % HeatmapDays;
			heatmap[dayIndex][hour] = Convert.ToInt32(reader.GetInt64(2));
		}

		return heatmap;
	}

	private static IReadOnlyList<(DateOnly Day, int Count)> ReadDayCounts(SqliteConnection connection)
	{
		var days = new List<(DateOnly Day, int Count)>();

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT date(datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS day,
			       COUNT(*)
			FROM listensHistory
			GROUP BY day
			ORDER BY day;
			""";

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			if (DateOnly.TryParse(reader.GetString(0), CultureInfo.InvariantCulture, out var day))
				days.Add((day, Convert.ToInt32(reader.GetInt64(1))));
		}

		return days;
	}

	private static (int Total, long TotalMs, int DistinctTracks, int DistinctArtists) ReadTotals(
		SqliteConnection connection)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COUNT(*),
			       COALESCE(SUM(t.DurationMs), 0),
			       COUNT(DISTINCT h.sourceId || ':' || h.trackId),
			       COUNT(DISTINCT NULLIF(TRIM(t.Artist), ''))
			FROM listensHistory h
			LEFT JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId;
			""";

		using var reader = cmd.ExecuteReader();
		if (!reader.Read())
			return (0, 0, 0, 0);

		return (
			Convert.ToInt32(reader.GetInt64(0)),
			reader.GetInt64(1),
			Convert.ToInt32(reader.GetInt64(2)),
			Convert.ToInt32(reader.GetInt64(3)));
	}

	/// <summary>
	/// Топ по текстовой колонке трека. Имя колонки — только из белого списка,
	/// потому что подставляется в SQL.
	/// </summary>
	private static IReadOnlyList<StatisticsEntry> ReadTopColumn(
		SqliteConnection connection,
		string column,
		int limit)
	{
		if (column is not ("Artist" or "Album"))
			throw new ArgumentOutOfRangeException(nameof(column));

		var result = new List<StatisticsEntry>();

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			$"""
			SELECT TRIM(t.{column}) AS name, COUNT(*) AS listens
			FROM listensHistory h
			JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId
			WHERE t.{column} IS NOT NULL AND TRIM(t.{column}) <> ''
			GROUP BY name
			ORDER BY listens DESC
			LIMIT $limit;
			""";
		cmd.Parameters.AddWithValue("$limit", limit);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
			result.Add(new StatisticsEntry(reader.GetString(0), Convert.ToInt32(reader.GetInt64(1))));

		return result;
	}

	/// <summary>Жанры хранятся JSON-массивом, поэтому раскладываем их в памяти.</summary>
	private static IReadOnlyList<StatisticsEntry> ReadTopGenres(SqliteConnection connection, int limit)
	{
		var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT t.Genres, COUNT(*) AS listens
			FROM listensHistory h
			JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId
			WHERE t.Genres IS NOT NULL AND t.Genres <> ''
			GROUP BY t.Genres;
			""";

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			string json = reader.GetString(0);
			int listens = Convert.ToInt32(reader.GetInt64(1));

			List<string>? genres;
			try
			{
				genres = JsonSerializer.Deserialize<List<string>>(json);
			}
			catch (JsonException)
			{
				continue;
			}

			foreach (string genre in genres ?? [])
			{
				if (string.IsNullOrWhiteSpace(genre))
					continue;

				string key = genre.Trim();
				totals[key] = totals.GetValueOrDefault(key) + listens;
			}
		}

		return totals
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
			.Take(limit)
			.Select(pair => new StatisticsEntry(pair.Key, pair.Value))
			.ToList();
	}

	private static IReadOnlyList<StatisticsEntry> ReadDecades(SqliteConnection connection, int limit)
	{
		var result = new List<StatisticsEntry>();

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT (t.Year / 10) * 10 AS decade, COUNT(*) AS listens
			FROM listensHistory h
			JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId
			WHERE t.Year BETWEEN 1900 AND 2100
			GROUP BY decade
			ORDER BY listens DESC
			LIMIT $limit;
			""";
		cmd.Parameters.AddWithValue("$limit", limit);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
			result.Add(new StatisticsEntry($"{reader.GetInt32(0)}-е", Convert.ToInt32(reader.GetInt64(1))));

		return result;
	}

	private static int? ReadAverageTrackYear(SqliteConnection connection)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT AVG(t.Year)
			FROM listensHistory h
			JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId
			WHERE t.Year BETWEEN 1900 AND 2100;
			""";

		object? result = cmd.ExecuteScalar();
		if (result is null or DBNull)
			return null;

		double average = Convert.ToDouble(result, CultureInfo.InvariantCulture);
		return (int)Math.Round(average);
	}

	private static (int Radio, int Local) ReadShares(SqliteConnection connection)
	{
		int radio = 0;
		int local = 0;

		using (var cmd = connection.CreateCommand())
		{
			cmd.CommandText = "SELECT context, COUNT(*) FROM listensHistory GROUP BY context;";
			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				if (string.Equals(reader.GetString(0), nameof(PlaylistContext.Radio), StringComparison.Ordinal))
					radio = Convert.ToInt32(reader.GetInt64(1));
			}
		}

		using (var cmd = connection.CreateCommand())
		{
			cmd.CommandText = "SELECT sourceId, COUNT(*) FROM listensHistory GROUP BY sourceId;";
			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				if (string.Equals(reader.GetString(0), LocalMusicSource.Id.Value, StringComparison.Ordinal))
					local = Convert.ToInt32(reader.GetInt64(1));
			}
		}

		return (radio, local);
	}

	private static (string? Track, int Count) ReadMostRepeatedInDay(SqliteConnection connection)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COALESCE(NULLIF(TRIM(t.Artist), ''), '') AS artist,
			       COALESCE(NULLIF(TRIM(t.Title), ''), '') AS title,
			       h.trackId,
			       COUNT(*) AS listens
			FROM listensHistory h
			LEFT JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId
			GROUP BY h.sourceId, h.trackId,
			         date(datetime(h.utcTime, '+' || h.utcOffsetMinutes || ' minutes'))
			ORDER BY listens DESC
			LIMIT 1;
			""";

		using var reader = cmd.ExecuteReader();
		if (!reader.Read())
			return (null, 0);

		string name = FormatTrackName(reader.GetString(0), reader.GetString(1), reader.GetString(2));
		return (name, Convert.ToInt32(reader.GetInt64(3)));
	}

	private static IReadOnlyList<ForgottenTrack> ReadForgottenTracks(SqliteConnection connection)
	{
		var result = new List<ForgottenTrack>();

		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COALESCE(NULLIF(TRIM(t.Artist), ''), '') AS artist,
			       COALESCE(NULLIF(TRIM(t.Title), ''), '') AS title,
			       h.trackId,
			       COUNT(*) AS listens,
			       MAX(date(datetime(h.utcTime, '+' || h.utcOffsetMinutes || ' minutes'))) AS lastListened
			FROM listensHistory h
			LEFT JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId
			GROUP BY h.sourceId, h.trackId
			HAVING listens >= $minListens AND lastListened < date('now', $window)
			ORDER BY listens DESC, lastListened ASC
			LIMIT $limit;
			""";
		cmd.Parameters.AddWithValue("$minListens", ForgottenMinListens);
		cmd.Parameters.AddWithValue("$window", $"-{ForgottenQuietDays} day");
		cmd.Parameters.AddWithValue("$limit", ForgottenLimit);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			if (!DateOnly.TryParse(reader.GetString(4), CultureInfo.InvariantCulture, out var lastListened))
				continue;

			result.Add(new ForgottenTrack(
				FormatTrackName(reader.GetString(0), reader.GetString(1), reader.GetString(2)),
				Convert.ToInt32(reader.GetInt64(3)),
				lastListened));
		}

		return result;
	}

	private static int ReadOneTimeTracks(SqliteConnection connection)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COUNT(*) FROM (
				SELECT 1 FROM listensHistory
				GROUP BY sourceId, trackId
				HAVING COUNT(*) = 1
			);
			""";

		return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
	}

	private static (string? Track, DateTime? ListenedAt) ReadFirstListen(SqliteConnection connection)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COALESCE(NULLIF(TRIM(t.Artist), ''), '') AS artist,
			       COALESCE(NULLIF(TRIM(t.Title), ''), '') AS title,
			       h.trackId,
			       h.utcTime
			FROM listensHistory h
			LEFT JOIN Tracks t ON t.SourceType = h.sourceId AND t.TrackId = h.trackId
			ORDER BY h.utcTime ASC
			LIMIT 1;
			""";

		using var reader = cmd.ExecuteReader();
		if (!reader.Read())
			return (null, null);

		string name = FormatTrackName(reader.GetString(0), reader.GetString(1), reader.GetString(2));

		DateTime? listenedAt = DateTime.TryParse(
			reader.GetString(3),
			CultureInfo.InvariantCulture,
			DateTimeStyles.RoundtripKind,
			out var parsed)
			? parsed.ToLocalTime()
			: null;

		return (name, listenedAt);
	}

	private static int ReadNewTracks(SqliteConnection connection, int windowDays)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COUNT(*) FROM (
				SELECT h.sourceId, h.trackId, MIN(h.utcTime) AS firstListen
				FROM listensHistory h
				GROUP BY h.sourceId, h.trackId
			)
			WHERE datetime(firstListen) >= datetime('now', $window);
			""";
		cmd.Parameters.AddWithValue("$window", $"-{windowDays} day");

		return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
	}

	// ── Вычисления ────────────────────────────────────────────────────────

	private static int FindPeakHour(int[] byHour)
	{
		int peak = -1;
		int best = 0;

		for (int hour = 0; hour < byHour.Length; hour++)
		{
			if (byHour[hour] <= best)
				continue;

			best = byHour[hour];
			peak = hour;
		}

		return peak;
	}

	private static string BuildListenerProfile(int[] byHour, int total)
	{
		if (total == 0)
			return "пока тишина";

		int night = SumRange(byHour, 0, 5);
		int morning = SumRange(byHour, 6, 11);
		int evening = SumRange(byHour, 18, 23);

		if (night * 4 >= total)
			return $"Сова: {Percent(night, total)}% прослушиваний после полуночи";

		if (evening * 5 >= total * 2)
			return $"Вечерний меломан: {Percent(evening, total)}% после 18:00";

		if (morning * 100 >= total * 35)
			return $"Жаворонок: {Percent(morning, total)}% до полудня";

		return $"Дневной слушатель: пик в {FindPeakHour(byHour):00}:00";
	}

	private static int SumRange(int[] values, int from, int to)
	{
		int sum = 0;
		for (int i = from; i <= to && i < values.Length; i++)
			sum += values[i];

		return sum;
	}

	private static int Percent(int part, int total)
		=> total == 0 ? 0 : (int)Math.Round(100.0 * part / total);

	private static (DateOnly? Day, int Listens) FindBestDay(IReadOnlyList<(DateOnly Day, int Count)> days)
	{
		if (days.Count == 0)
			return (null, 0);

		var best = days.MaxBy(day => day.Count);
		return (best.Day, best.Count);
	}

	private static (int Current, int Longest) ComputeStreaks(IReadOnlyList<(DateOnly Day, int Count)> days)
	{
		if (days.Count == 0)
			return (0, 0);

		int longest = 1;
		int run = 1;

		for (int i = 1; i < days.Count; i++)
		{
			run = days[i].Day.DayNumber - days[i - 1].Day.DayNumber == 1 ? run + 1 : 1;
			longest = Math.Max(longest, run);
		}

		var today = DateOnly.FromDateTime(DateTime.Now);
		int current = days[^1].Day.DayNumber >= today.DayNumber - 1 ? 1 : 0;

		if (current > 0)
		{
			for (int i = days.Count - 1; i > 0; i--)
			{
				if (days[i].Day.DayNumber - days[i - 1].Day.DayNumber != 1)
					break;

				current++;
			}
		}

		return (current, longest);
	}

	private static string FormatTrackName(string artist, string title, string trackId)
	{
		if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
			return $"{artist} — {title}";

		return !string.IsNullOrWhiteSpace(title) ? title : trackId;
	}
}
