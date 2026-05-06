using Microsoft.Data.Sqlite;
using Moq;

namespace YamBassPlayer.Tests.Data;

using YamBassPlayer.Enums;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class HistoryServiceTests
{
    private SqliteConnection _connection = null!;
    private Mock<IDbWriteLock> _mockLock = null!;
    private HistoryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _mockLock = new Mock<IDbWriteLock>();
        _mockLock
            .Setup(l => l.AcquireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDisposable>());

        _service = new HistoryService(_connection, _mockLock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
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
    public void LogListen_InsertsRow()
    {
        _service.LogListen("track123", ListenSource.Regular);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM listensHistory WHERE trackId = 'track123';";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.That(count, Is.EqualTo(1));
    }

    // ── GetTopTracks ──────────────────────────────────────────────────────

    [Test]
    public void GetTopTracks_ReturnsOrderedByCount()
    {
        _service.LogListen("trackA", ListenSource.Regular);
        _service.LogListen("trackA", ListenSource.Regular);
        _service.LogListen("trackB", ListenSource.Regular);
        _service.LogListen("trackC", ListenSource.Regular);

        var top = _service.GetTopTracks(10);

        Assert.That(top[0].trackId, Is.EqualTo("trackA"));
        Assert.That(top[0].count, Is.EqualTo(2));
        Assert.That(top[1].count, Is.EqualTo(1));
        Assert.That(top[2].count, Is.EqualTo(1));
    }

    // ── GetTopEveningTracks ───────────────────────────────────────────────

    [Test]
    public void GetTopEveningTracks_FiltersByEveningHours()
    {
        // Insert rows with UTC hour 18 and 20 (evening, local time = UTC)
        InsertListenRow("evening_track", "2024-01-15T18:00:00.0000000Z", 0, ListenSource.Regular);
        InsertListenRow("evening_track", "2024-01-16T20:30:00.0000000Z", 0, ListenSource.Regular);
        // Insert a daytime row (hour 10) — should not appear in results
        InsertListenRow("day_track", "2024-01-15T10:00:00.0000000Z", 0, ListenSource.Regular);

        var top = _service.GetTopEveningTracks(10);

        Assert.That(top, Has.Count.EqualTo(1));
        Assert.That(top[0].trackId, Is.EqualTo("evening_track"));
        Assert.That(top[0].count, Is.EqualTo(2));
    }

    // ── GetTopTracksByDayOfWeek ───────────────────────────────────────────

    [Test]
    public void GetTopTracksByDayOfWeek_FiltersByDay()
    {
        // January 20, 2025 was a Monday
        InsertListenRow("monday_track", "2025-01-20T18:00:00.0000000Z", 0, ListenSource.Regular);
        InsertListenRow("monday_track", "2025-01-20T20:00:00.0000000Z", 0, ListenSource.Regular);
        // January 21, 2025 was a Tuesday
        InsertListenRow("tuesday_track", "2025-01-21T18:00:00.0000000Z", 0, ListenSource.Regular);

        var mondayTop = _service.GetTopTracksByDayOfWeek(DayOfWeek.Monday, 50);

        Assert.That(mondayTop, Has.Count.EqualTo(1));
        Assert.That(mondayTop[0].trackId, Is.EqualTo("monday_track"));
        Assert.That(mondayTop[0].count, Is.EqualTo(2));
    }

    // ── GetListenCount ────────────────────────────────────────────────────

    [Test]
    public void GetListenCount_ReturnsCorrectCount()
    {
        _service.LogListen("trackX", ListenSource.Regular);
        _service.LogListen("trackX", ListenSource.Regular);
        _service.LogListen("trackX", ListenSource.MyWave);
        _service.LogListen("trackY", ListenSource.Regular);

        var count = _service.GetListenCount("trackX");
        Assert.That(count, Is.EqualTo(3));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a row directly into listensHistory with full control over values.
    /// </summary>
    private void InsertListenRow(
        string trackId,
        string utcTime,
        int utcOffsetMinutes,
        ListenSource source)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO listensHistory (trackId, utcTime, utcOffsetMinutes, source)
            VALUES ($trackId, $utcTime, $utcOffsetMinutes, $source);
            """;
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.Parameters.AddWithValue("$utcTime", utcTime);
        cmd.Parameters.AddWithValue("$utcOffsetMinutes", utcOffsetMinutes);
        cmd.Parameters.AddWithValue("$source", source.ToString());
        cmd.ExecuteNonQuery();
    }
}
