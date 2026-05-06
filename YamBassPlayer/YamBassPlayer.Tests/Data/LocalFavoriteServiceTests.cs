using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Tests.Data;

using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class LocalFavoriteServiceTests
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

    // ── Constructor ───────────────────────────────────────────────────────

    [Test]
    public void Constructor_CreatesTable()
    {
        // EnsureSchema() runs synchronously in the constructor
        _ = new LocalFavoriteService(_connection);

        Assert.That(
            SqliteSchemaHelper.HasTable(_connection, "favoriteLocalTracks"),
            Is.True);
    }

    [Test]
    public async Task Constructor_LoadsExistingFavorites()
    {
        // Pre-populate the table with known tracks
        InsertFavoriteTrackDirect("preloaded_1", 1000);
        InsertFavoriteTrackDirect("preloaded_2", 2000);

        var service = new LocalFavoriteService(_connection);

        // Wait for LoadFavorites (runs on ThreadPool) to complete
        await WaitForFavoritesLoaded(service, "preloaded_1", TimeSpan.FromSeconds(3));

        Assert.Multiple(() =>
        {
            Assert.That(service.IsTrackFavorite("preloaded_1"), Is.True);
            Assert.That(service.IsTrackFavorite("preloaded_2"), Is.True);
            Assert.That(service.IsTrackFavorite("nonexistent"), Is.False);
        });
    }

    // ── AddToFavorites ────────────────────────────────────────────────────

    [Test]
    public async Task AddToFavorites_InsertsTrack_AndIsTrackFavoriteReturnsTrue()
    {
        var service = new LocalFavoriteService(_connection);

        await service.AddToFavorites("test123");

        Assert.Multiple(() =>
        {
            Assert.That(service.IsTrackFavorite("test123"), Is.True);

            // Verify it's actually in the database
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM favoriteLocalTracks WHERE trackId = 'test123';";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AddToFavorites_FiresOnFavoriteAdded()
    {
        var service = new LocalFavoriteService(_connection);
        var eventFired = false;
        string? firedTrackId = null;

        service.OnFavoriteAdded += (id) =>
        {
            eventFired = true;
            firedTrackId = id;
        };

        await service.AddToFavorites("test_event");

        Assert.Multiple(() =>
        {
            Assert.That(eventFired, Is.True);
            Assert.That(firedTrackId, Is.EqualTo("test_event"));
        });
    }

    [Test]
    public async Task AddToFavorites_IsIdempotent()
    {
        var service = new LocalFavoriteService(_connection);

        await service.AddToFavorites("dup_test");
        // Second add should not throw and should have no effect
        await service.AddToFavorites("dup_test");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM favoriteLocalTracks WHERE trackId = 'dup_test';";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.That(count, Is.EqualTo(1));
    }

    // ── RemoveFromFavorites ───────────────────────────────────────────────

    [Test]
    public async Task RemoveFromFavorites_DeletesTrack_AndIsTrackFavoriteReturnsFalse()
    {
        var service = new LocalFavoriteService(_connection);

        await service.AddToFavorites("remove_me");

        await service.RemoveFromFavorites("remove_me");

        Assert.Multiple(() =>
        {
            Assert.That(service.IsTrackFavorite("remove_me"), Is.False);

            // Verify it's gone from the database
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM favoriteLocalTracks WHERE trackId = 'remove_me';";
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RemoveFromFavorites_FiresOnFavoriteRemoved()
    {
        var service = new LocalFavoriteService(_connection);
        await service.AddToFavorites("remove_event");

        var eventFired = false;
        string? firedTrackId = null;

        service.OnFavoriteRemoved += (id) =>
        {
            eventFired = true;
            firedTrackId = id;
        };

        await service.RemoveFromFavorites("remove_event");

        Assert.Multiple(() =>
        {
            Assert.That(eventFired, Is.True);
            Assert.That(firedTrackId, Is.EqualTo("remove_event"));
        });
    }

    [Test]
    public async Task RemoveFromFavorites_IsIdempotent_OnNonExisting()
    {
        var service = new LocalFavoriteService(_connection);

        // Removing a non-existing track should not throw
        Assert.DoesNotThrowAsync(async () =>
            await service.RemoveFromFavorites("never_added"));
    }

    // ── GetAllFavoriteTrackIds ────────────────────────────────────────────

    [Test]
    public async Task GetAllFavoriteTrackIds_ReturnsAllInDescOrder()
    {
        // Pre-populate the table with known timestamps
        InsertFavoriteTrackDirect("track_a", 1000);
        InsertFavoriteTrackDirect("track_b", 2000);
        InsertFavoriteTrackDirect("track_c", 3000);

        var service = new LocalFavoriteService(_connection);

        var allIds = await service.GetAllFavoriteTrackIds();

        Assert.That(allIds, Is.EqualTo(new[] { "track_c", "track_b", "track_a" }));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a row directly into favoriteLocalTracks (creates the table first if needed).
    /// </summary>
    private void InsertFavoriteTrackDirect(string trackId, long addedAt)
    {
        using var createCmd = _connection.CreateCommand();
        createCmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS favoriteLocalTracks (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                trackId TEXT    UNIQUE NOT NULL,
                addedAt INTEGER NOT NULL
            );
            """;
        createCmd.ExecuteNonQuery();

        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText =
            """
            INSERT OR IGNORE INTO favoriteLocalTracks (trackId, addedAt)
            VALUES ($trackId, $addedAt);
            """;
        insertCmd.Parameters.AddWithValue("$trackId", trackId);
        insertCmd.Parameters.AddWithValue("$addedAt", addedAt);
        insertCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Waits for LoadFavorites to complete by polling IsTrackFavorite with a timeout.
    /// </summary>
    private static async Task WaitForFavoritesLoaded(
        LocalFavoriteService service,
        string knownTrackId,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (service.IsTrackFavorite(knownTrackId))
                return;

            try
            {
                await Task.Delay(20, cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"LoadFavorites did not complete within {timeout.TotalSeconds}s. " +
            $"Track '{knownTrackId}' was not loaded.");
    }
}
