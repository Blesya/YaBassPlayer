using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Data;

/// <summary>
/// Статистика прослушиваний считается из <c>listensHistory</c> и метаданных <c>Tracks</c>.
/// Времена суток проверяются через <c>utcOffsetMinutes</c>, стрики — на днях вокруг «сегодня».
/// </summary>
[TestFixture]
public sealed class ListeningStatisticsServiceTests
{
    private SharedMemoryDbConnectionFactory _factory = null!;
    private SqliteConnection _connection = null!;
    private ListeningStatisticsService _service = null!;
    private string _stateDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new SharedMemoryDbConnectionFactory();
        _connection = _factory.Anchor;
        _stateDirectory = Path.Combine(Path.GetTempPath(), $"listening_stats_{Guid.NewGuid():N}");

        new DatabaseInitializer(_factory, new PluginStateFile(_stateDirectory)).Initialize();

        _service = new ListeningStatisticsService(_factory);
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();

        if (Directory.Exists(_stateDirectory))
            Directory.Delete(_stateDirectory, recursive: true);
    }

    [Test]
    public void Collect_EmptyHistory_ReturnsQuietProfile()
    {
        ListeningStatistics stats = _service.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(stats.TotalListens, Is.Zero);
            Assert.That(stats.PeakHour, Is.EqualTo(-1));
            Assert.That(stats.ListenerProfile, Is.EqualTo("пока тишина"));
            Assert.That(stats.ListensByHour, Has.All.Zero);
            Assert.That(stats.Heatmap, Has.Length.EqualTo(7));
            Assert.That(stats.Heatmap.SelectMany(row => row), Has.All.Zero);
            Assert.That(stats.TopArtists, Is.Empty);
            Assert.That(stats.FirstTrack, Is.Null);
        });
    }

    [Test]
    public void Collect_UsesLocalTimeForHoursAndWeekdays()
    {
        InsertTrack("t1", "Кино", "Группа крови");
        InsertTrack("t2", "Аквариум", "Поезд");

        // 20 января 2025 — понедельник. Смещение +180 минут: 18:00 UTC → 21:00 понедельника.
        InsertListen("t1", "2025-01-20T18:00:00.0000000", utcOffsetMinutes: 180);
        InsertListen("t1", "2025-01-20T18:30:00.0000000", utcOffsetMinutes: 180);
        // Вторник, 06:30 по UTC без смещения.
        InsertListen("t2", "2025-01-21T06:30:00.0000000", utcOffsetMinutes: 0);

        ListeningStatistics stats = _service.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(stats.ListensByHour[21], Is.EqualTo(2));
            Assert.That(stats.ListensByHour[6], Is.EqualTo(1));
            Assert.That(stats.PeakHour, Is.EqualTo(21));
            Assert.That(stats.Heatmap[0][21], Is.EqualTo(2), "понедельник 21:00");
            Assert.That(stats.Heatmap[1][6], Is.EqualTo(1), "вторник 06:00");
            Assert.That(stats.ListenerProfile, Does.Contain("меломан"));
        });
    }

    [Test]
    public void Collect_AggregatesArtistsAlbumsGenresAndDuration()
    {
        InsertTrack("a", "Кино", "Группа крови", "Группа крови", year: 1988, durationMs: 300_000, genresJson: """["рок"]""");
        InsertTrack("b", "Кино", "Звезда", "Группа крови", year: 1988, durationMs: 240_000, genresJson: """["рок"]""");
        InsertTrack("c", "Аквариум", "Поезд", "Радио Африка", year: 1983, durationMs: 180_000, genresJson: """["рок","поп"]""");

        InsertListen("a", "2025-02-01T10:00:00.0000000", 0);
        InsertListen("a", "2025-02-01T10:05:00.0000000", 0);
        InsertListen("a", "2025-02-01T10:10:00.0000000", 0);
        InsertListen("b", "2025-02-01T10:15:00.0000000", 0);
        InsertListen("c", "2025-02-01T10:20:00.0000000", 0);

        ListeningStatistics stats = _service.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(stats.TotalListens, Is.EqualTo(5));
            Assert.That(stats.TotalListeningMs, Is.EqualTo(1_320_000));
            Assert.That(stats.DistinctTracks, Is.EqualTo(3));
            Assert.That(stats.DistinctArtists, Is.EqualTo(2));
            Assert.That(stats.AverageTrackYear, Is.EqualTo(1987));
            Assert.That(stats.TopArtists.Select(a => (a.Name, a.Count)),
                Is.EqualTo(new[] { ("Кино", 4), ("Аквариум", 1) }));
            Assert.That(stats.TopAlbums[0].Name, Is.EqualTo("Группа крови"));
            Assert.That(stats.TopAlbums[0].Count, Is.EqualTo(4));
            Assert.That(stats.TopGenres.Select(g => (g.Name, g.Count)),
                Is.EqualTo(new[] { ("рок", 5), ("поп", 1) }));
            Assert.That(stats.Decades[0].Name, Is.EqualTo("1980-е"));
            Assert.That(stats.FavoriteArtistListens, Is.EqualTo(4));
        });
    }

    [Test]
    public void Collect_ComputesStreaksAndBestDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        InsertListenOnDay(today.AddDays(-2), 12, "a");
        InsertListenOnDay(today.AddDays(-2), 13, "a");
        InsertListenOnDay(today.AddDays(-2), 14, "a");
        InsertListenOnDay(today.AddDays(-1), 12, "a");
        InsertListenOnDay(today, 12, "a");
        InsertListenOnDay(today, 13, "a");

        ListeningStatistics stats = _service.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(stats.ActiveDays, Is.EqualTo(3));
            Assert.That(stats.LongestStreakDays, Is.EqualTo(3));
            Assert.That(stats.CurrentStreakDays, Is.EqualTo(3));
            Assert.That(stats.BestDay, Is.EqualTo(today.AddDays(-2)));
            Assert.That(stats.BestDayListens, Is.EqualTo(3));
            Assert.That(stats.AverageListensPerActiveDay, Is.EqualTo(2));
        });
    }

    [Test]
    public void Collect_SeparatesRadioAndLocalSources()
    {
        InsertTrack("a", "Кино", "Группа крови");
        InsertTrack("b", "Аквариум", "Поезд");

        InsertListen("a", "2025-03-01T10:00:00.0000000", 0, context: "Radio");
        InsertListen("b", "2025-03-01T11:00:00.0000000", 0, context: "Regular");
        InsertListen(@"C:\music\local.mp3", "2025-03-01T12:00:00.0000000", 0, sourceId: "local");

        ListeningStatistics stats = _service.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(stats.RadioListens, Is.EqualTo(1));
            Assert.That(stats.LocalListens, Is.EqualTo(1));
            Assert.That(stats.FirstTrack, Is.EqualTo("Кино — Группа крови"));
            Assert.That(stats.OneTimeTracks, Is.EqualTo(3));
            Assert.That(stats.NewTracksLast30Days, Is.Zero);
        });
    }

    [Test]
    public void Collect_FindsRepeatsAndForgottenTracks()
    {
        InsertTrack("loop", "Кино", "Группа крови");
        InsertTrack("old", "Аквариум", "Поезд");

        for (int i = 0; i < 5; i++)
            InsertListen("loop", "2024-05-01T18:00:00.0000000", 0);

        for (int i = 0; i < 4; i++)
            InsertListen("old", "2024-05-02T18:00:00.0000000", 0);

        ListeningStatistics stats = _service.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(stats.MostRepeatedTrackInDay, Is.EqualTo("Кино — Группа крови"));
            Assert.That(stats.MostRepeatedTrackInDayCount, Is.EqualTo(5));
            Assert.That(stats.ForgottenTracks, Has.Count.EqualTo(2));
            Assert.That(stats.ForgottenTracks[0].Name, Is.EqualTo("Кино — Группа крови"));
            Assert.That(stats.ForgottenTracks[0].Listens, Is.EqualTo(5));
            Assert.That(stats.ForgottenTracks[0].LastListened, Is.EqualTo(new DateOnly(2024, 5, 1)));
        });
    }

    [Test]
    public void Collect_CountsNewTracksInLast30Days()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        InsertTrack("fresh", "Кино", "Группа крови");
        InsertTrack("stale", "Аквариум", "Поезд");

        InsertListenOnDay(today, 10, "fresh");
        InsertListen("stale", "2024-01-01T10:00:00.0000000", 0);

        ListeningStatistics stats = _service.Collect();

        Assert.Multiple(() =>
        {
            Assert.That(stats.NewTracksLast30Days, Is.EqualTo(1));
            Assert.That(stats.OneTimeTracks, Is.EqualTo(2));
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void InsertTrack(
        string trackId,
        string artist,
        string title,
        string album = "",
        int? year = null,
        long? durationMs = null,
        string? genresJson = null,
        string sourceType = "yandex")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO Tracks (TrackId, Artist, Title, Album, Year, DurationMs, Genres, SourceType, UpdatedAt)
            VALUES ($trackId, $artist, $title, $album, $year, $durationMs, $genres, $sourceType, 0);
            """;
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.Parameters.AddWithValue("$artist", artist);
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$album", album);
        cmd.Parameters.AddWithValue("$year", (object?)year ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$durationMs", (object?)durationMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$genres", (object?)genresJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sourceType", sourceType);
        cmd.ExecuteNonQuery();
    }

    private void InsertListen(
        string trackId,
        string utcTime,
        int utcOffsetMinutes,
        string context = "Regular",
        string sourceId = "yandex")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO listensHistory (trackId, sourceId, context, utcTime, utcOffsetMinutes)
            VALUES ($trackId, $sourceId, $context, $utcTime, $utcOffsetMinutes);
            """;
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.Parameters.AddWithValue("$sourceId", sourceId);
        cmd.Parameters.AddWithValue("$context", context);
        cmd.Parameters.AddWithValue("$utcTime", utcTime);
        cmd.Parameters.AddWithValue("$utcOffsetMinutes", utcOffsetMinutes);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Пишет прослушивание на конкретный локальный день с учётом смещения машины.</summary>
    private void InsertListenOnDay(DateOnly day, int hour, string trackId)
    {
        int offsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;
        DateTime utc = day.ToDateTime(new TimeOnly(hour, 0)).AddMinutes(-offsetMinutes);

        InsertListen(trackId, utc.ToString("O"), offsetMinutes);
    }
}
