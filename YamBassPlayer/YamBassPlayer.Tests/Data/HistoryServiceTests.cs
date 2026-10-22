using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Tests.Data;

using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class HistoryServiceTests
{
    private static readonly TrackKey YandexTrack = new(YandexMusicSourceId.Value, "track123");
    private static readonly TrackKey LocalTrack = new(LocalMusicSource.Id, @"C:\music\track.mp3");

    private SharedMemoryDbConnectionFactory _factory = null!;
    private SqliteConnection _connection = null!;
    private HistoryService _service = null!;
    private string _stateDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new SharedMemoryDbConnectionFactory();
        _connection = _factory.Anchor;
        _stateDirectory = Path.Combine(Path.GetTempPath(), $"history_state_{Guid.NewGuid():N}");

        // Schema is now owned by DatabaseInitializer, not by HistoryService's constructor.
        new DatabaseInitializer(_factory, new PluginStateFile(_stateDirectory)).Initialize();

        _service = new HistoryService(_factory);
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();

        if (Directory.Exists(_stateDirectory))
            Directory.Delete(_stateDirectory, recursive: true);
    }

    // ── Constructor ───────────────────────────────────────────────────────

    [Test]
    public void Constructor_CreatesSchemaTables()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SqliteSchemaHelper.HasTable(_connection, "listensHistory"), Is.True);
            Assert.That(SqliteSchemaHelper.HasTable(_connection, "Artists"), Is.True);
            Assert.That(SqliteSchemaHelper.HasTable(_connection, "Albums"), Is.True);
            Assert.That(SqliteSchemaHelper.HasTable(_connection, "TrackArtists"), Is.True);
            Assert.That(SqliteSchemaHelper.HasTable(_connection, "LocalFolders"), Is.True);
        });
    }

    // ── LogListen ─────────────────────────────────────────────────────────

    [Test]
    public void LogListen_StoresSourceAndContext()
    {
        _service.LogListen(YandexTrack, PlaylistContext.Radio);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT trackId, sourceId, context FROM listensHistory WHERE trackId = 'track123';";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("track123"));
            Assert.That(reader.GetString(1), Is.EqualTo("yandex"));
            Assert.That(reader.GetString(2), Is.EqualTo("Radio"));
        });
    }

    [Test]
    public void LogListen_KeepsLocalSourceForLocalTrack()
    {
        _service.LogListen(LocalTrack, PlaylistContext.Regular);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT sourceId, context FROM listensHistory;";
        using var reader = cmd.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("local"));
            Assert.That(reader.GetString(1), Is.EqualTo("Regular"));
        });
    }

    // ── GetTopTracks ──────────────────────────────────────────────────────

    [Test]
    public void GetTopTracks_GroupsBySourceAndTrackId()
    {
        // Один и тот же идентификатор из двух источников — две разные записи в топе.
        _service.LogListen(new TrackKey(YandexMusicSourceId.Value, "42"), PlaylistContext.Regular);
        _service.LogListen(new TrackKey(YandexMusicSourceId.Value, "42"), PlaylistContext.Regular);
        _service.LogListen(new TrackKey(LocalMusicSource.Id, "42"), PlaylistContext.Regular);

        var top = _service.GetTopTracks(10);

        Assert.Multiple(() =>
        {
            Assert.That(top, Has.Count.EqualTo(2));
            Assert.That(top[0].key, Is.EqualTo(new TrackKey(YandexMusicSourceId.Value, "42")));
            Assert.That(top[0].count, Is.EqualTo(2));
            Assert.That(top[1].key, Is.EqualTo(new TrackKey(LocalMusicSource.Id, "42")));
            Assert.That(top[1].count, Is.EqualTo(1));
        });
    }

    // ── GetTopEveningTracks ───────────────────────────────────────────────

    [Test]
    public void GetTopEveningTracks_FiltersByEveningHours()
    {
        // Insert rows with UTC hour 18 and 20 (evening, local time = UTC)
        InsertListenRow("evening_track", "2024-01-15T18:00:00.0000000Z", 0, "yandex", "Regular");
        InsertListenRow("evening_track", "2024-01-16T20:30:00.0000000Z", 0, "yandex", "Regular");
        // Insert a daytime row (hour 10) — should not appear in results
        InsertListenRow("day_track", "2024-01-15T10:00:00.0000000Z", 0, "yandex", "Regular");

        var top = _service.GetTopEveningTracks(10);

        Assert.That(top, Has.Count.EqualTo(1));
        Assert.That(top[0].key, Is.EqualTo(new TrackKey(YandexMusicSourceId.Value, "evening_track")));
        Assert.That(top[0].count, Is.EqualTo(2));
    }

    // ── GetTopTracksByDayOfWeek ───────────────────────────────────────────

    [Test]
    public void GetTopTracksByDayOfWeek_ReturnsKeys()
    {
        // January 20, 2025 was a Monday
        InsertListenRow("monday_track", "2025-01-20T18:00:00.0000000Z", 0, "yandex", "Regular");
        InsertListenRow("monday_track", "2025-01-20T20:00:00.0000000Z", 0, "local", "Regular");
        // January 21, 2025 was a Tuesday
        InsertListenRow("tuesday_track", "2025-01-21T18:00:00.0000000Z", 0, "yandex", "Regular");

        var mondayTop = _service.GetTopTracksByDayOfWeek(DayOfWeek.Monday, 50);

        Assert.Multiple(() =>
        {
            Assert.That(mondayTop, Has.Count.EqualTo(2));
            Assert.That(
                mondayTop.Select(x => x.key),
                Is.EquivalentTo(new[]
                {
                    new TrackKey(YandexMusicSourceId.Value, "monday_track"),
                    new TrackKey(LocalMusicSource.Id, "monday_track"),
                }));
            Assert.That(mondayTop.All(x => x.count == 1), Is.True);
        });
    }

    // ── GetListenCount ────────────────────────────────────────────────────

    [Test]
    public void GetListenCount_IsPerSource()
    {
        _service.LogListen(new TrackKey(YandexMusicSourceId.Value, "trackX"), PlaylistContext.Regular);
        _service.LogListen(new TrackKey(YandexMusicSourceId.Value, "trackX"), PlaylistContext.Regular);
        _service.LogListen(new TrackKey(YandexMusicSourceId.Value, "trackX"), PlaylistContext.Radio);
        _service.LogListen(new TrackKey(LocalMusicSource.Id, "trackX"), PlaylistContext.Regular);

        Assert.Multiple(() =>
        {
            Assert.That(
                _service.GetListenCount(new TrackKey(YandexMusicSourceId.Value, "trackX")),
                Is.EqualTo(3));
            Assert.That(
                _service.GetListenCount(new TrackKey(LocalMusicSource.Id, "trackX")),
                Is.EqualTo(1));
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a row directly into listensHistory with full control over values.
    /// </summary>
    private void InsertListenRow(
        string trackId,
        string utcTime,
        int utcOffsetMinutes,
        string sourceId,
        string context)
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
}
