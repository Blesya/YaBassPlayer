using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Tests.Data;

using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class SqliteSchemaHelperTests
{
    private SqliteConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    // ── HasTable ──────────────────────────────────────────────────────────

    [Test]
    public void HasTable_ReturnsTrue_WhenTableExists()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_table (id INTEGER);";
        cmd.ExecuteNonQuery();

        Assert.That(SqliteSchemaHelper.HasTable(_connection, "test_table"), Is.True);
    }

    [Test]
    public void HasTable_ReturnsFalse_WhenTableDoesNotExist()
    {
        Assert.That(SqliteSchemaHelper.HasTable(_connection, "nonexistent"), Is.False);
    }

    // ── HasColumn ─────────────────────────────────────────────────────────

    [Test]
    public void HasColumn_ReturnsTrue_WhenColumnExists()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_table (id INTEGER, name TEXT);";
        cmd.ExecuteNonQuery();

        Assert.That(SqliteSchemaHelper.HasColumn(_connection, "test_table", "name"), Is.True);
    }

    [Test]
    public void HasColumn_ReturnsFalse_WhenColumnDoesNotExist()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE test_table (id INTEGER);";
        cmd.ExecuteNonQuery();

        Assert.That(SqliteSchemaHelper.HasColumn(_connection, "test_table", "missing_col"), Is.False);
    }

    [Test]
    public void HasColumn_ReturnsFalse_WhenTableDoesNotExist()
    {
        Assert.That(SqliteSchemaHelper.HasColumn(_connection, "nonexistent", "any"), Is.False);
    }

    // ── EnsureTrackColumn ─────────────────────────────────────────────────

    [Test]
    public void EnsureTrackColumn_AddsColumn_WhenMissing()
    {
        using var createCmd = _connection.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Tracks (id INTEGER);";
        createCmd.ExecuteNonQuery();

        Assert.That(SqliteSchemaHelper.HasColumn(_connection, "Tracks", "NewCol"), Is.False);

        SqliteSchemaHelper.EnsureTrackColumn(_connection, "NewCol", "TEXT DEFAULT ''");

        Assert.That(SqliteSchemaHelper.HasColumn(_connection, "Tracks", "NewCol"), Is.True);
    }

    [Test]
    public void EnsureTrackColumn_DoesNothing_WhenAlreadyPresent()
    {
        using var createCmd = _connection.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Tracks (id INTEGER, ExistingCol TEXT DEFAULT '');";
        createCmd.ExecuteNonQuery();

        // Act — should not throw
        SqliteSchemaHelper.EnsureTrackColumn(_connection, "ExistingCol", "TEXT DEFAULT ''");

        Assert.That(SqliteSchemaHelper.HasColumn(_connection, "Tracks", "ExistingCol"), Is.True);
    }

    [Test]
    public void EnsureTrackColumn_DoesNothing_WhenTableMissing()
    {
        // No Tracks table exists — should not throw
        Assert.DoesNotThrow(() =>
            SqliteSchemaHelper.EnsureTrackColumn(_connection, "SomeCol", "TEXT"));
    }

    // ── EnsureTableIndex ──────────────────────────────────────────────────

    [Test]
    public void EnsureTableIndex_CreatesIndex()
    {
        using var createCmd = _connection.CreateCommand();
        createCmd.CommandText = "CREATE TABLE test_table (id INTEGER, name TEXT);";
        createCmd.ExecuteNonQuery();

        SqliteSchemaHelper.EnsureTableIndex(_connection, "idx_test_name", "test_table", "name");

        using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'index' AND name = 'idx_test_name';";
        Assert.That(checkCmd.ExecuteScalar(), Is.Not.Null);
    }

    [Test]
    public void EnsureTableIndex_IsIdempotent()
    {
        using var createCmd = _connection.CreateCommand();
        createCmd.CommandText = "CREATE TABLE test_table (id INTEGER, name TEXT);";
        createCmd.ExecuteNonQuery();

        SqliteSchemaHelper.EnsureTableIndex(_connection, "idx_test_name", "test_table", "name");

        // Second call should not throw
        Assert.DoesNotThrow(() =>
            SqliteSchemaHelper.EnsureTableIndex(_connection, "idx_test_name", "test_table", "name"));
    }

    // ── BackfillTrackCoverMetadataColumns ─────────────────────────────────

    [Test]
    public void BackfillTrackCoverMetadataColumns_UpdatesRemoteCoverUrl_ForNonLocalTracks()
    {
        CreateTracksTableWithCoverColumns();

        InsertTrack("track1", "http://example.com/cover1.jpg", "yandex", null, null);
        InsertTrack("track2", "http://example.com/cover2.jpg", null, null, null);
        InsertTrack("track3", "http://example.com/cover3.jpg", "", null, null);

        SqliteSchemaHelper.BackfillTrackCoverMetadataColumns(_connection);

        AssertTrackCoverValue("track1", "RemoteCoverUrl", "http://example.com/cover1.jpg");
        AssertTrackCoverValue("track2", "RemoteCoverUrl", "http://example.com/cover2.jpg");
        AssertTrackCoverValue("track3", "RemoteCoverUrl", "http://example.com/cover3.jpg");
    }

    [Test]
    public void BackfillTrackCoverMetadataColumns_UpdatesLocalCoverPath_ForLocalTracks()
    {
        CreateTracksTableWithCoverColumns();

        InsertTrack("track1", @"C:\music\cover.jpg", "local", null, null);
        InsertTrack("track2", @"D:\music\cover.png", "local", null, null);

        SqliteSchemaHelper.BackfillTrackCoverMetadataColumns(_connection);

        AssertTrackCoverValue("track1", "LocalCoverPath", @"C:\music\cover.jpg");
        AssertTrackCoverValue("track2", "LocalCoverPath", @"D:\music\cover.png");
    }

    [Test]
    public void BackfillTrackCoverMetadataColumns_NoOp_WhenTablesColumnsMissing()
    {
        // Create Tracks without CoverUrl column — backfill should be a no-op
        using var createCmd = _connection.CreateCommand();
        createCmd.CommandText = "CREATE TABLE Tracks (id INTEGER);";
        createCmd.ExecuteNonQuery();

        Assert.DoesNotThrow(() =>
            SqliteSchemaHelper.BackfillTrackCoverMetadataColumns(_connection));
    }

    [Test]
    public void BackfillTrackCoverMetadataColumns_DoesNotOverwriteExistingValues()
    {
        CreateTracksTableWithCoverColumns();

        InsertTrack("track1", "http://example.com/cover.jpg", "yandex",
            "http://example.com/existing_remote.jpg", null);

        SqliteSchemaHelper.BackfillTrackCoverMetadataColumns(_connection);

        // Should keep the existing RemoteCoverUrl, not overwrite with CoverUrl
        AssertTrackCoverValue("track1", "RemoteCoverUrl",
            "http://example.com/existing_remote.jpg");
    }

    [Test]
    public void BackfillTrackCoverMetadataColumns_NoOp_WhenTracksTableDoesNotExist()
    {
        Assert.DoesNotThrow(() =>
            SqliteSchemaHelper.BackfillTrackCoverMetadataColumns(_connection));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the Tracks table with CoverUrl, SourceType, RemoteCoverUrl, and LocalCoverPath columns.
    /// </summary>
    private void CreateTracksTableWithCoverColumns()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE Tracks (
                trackId        TEXT PRIMARY KEY,
                CoverUrl       TEXT,
                SourceType     TEXT,
                RemoteCoverUrl TEXT,
                LocalCoverPath TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts a row into the Tracks table with the specified values.
    /// </summary>
    private void InsertTrack(
        string trackId,
        string? coverUrl,
        string? sourceType,
        string? remoteCoverUrl,
        string? localCoverPath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO Tracks (trackId, CoverUrl, SourceType, RemoteCoverUrl, LocalCoverPath)
            VALUES ($trackId, $coverUrl, $sourceType, $remoteCoverUrl, $localCoverPath);
            """;
        cmd.Parameters.AddWithValue("$trackId", trackId);
        cmd.Parameters.AddWithValue("$coverUrl", (object?)coverUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sourceType", (object?)sourceType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$remoteCoverUrl", (object?)remoteCoverUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$localCoverPath", (object?)localCoverPath ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Asserts that a specific column on a track has the expected string value.
    /// </summary>
    private void AssertTrackCoverValue(string trackId, string column, string expected)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT {column} FROM Tracks WHERE trackId = $trackId;";
        cmd.Parameters.AddWithValue("$trackId", trackId);
        var result = cmd.ExecuteScalar();
        Assert.That(result, Is.EqualTo(expected));
    }
}
