using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Data;

[TestFixture]
public sealed class LocalFavoriteServiceTests
{
    private static readonly TrackKey Local = new(LocalMusicSource.Id, "/music/a.mp3");
    private static readonly TrackKey Yandex = new(new SourceId("yandex"), "12345");

    private SharedMemoryDbConnectionFactory _factory = null!;
    private DbWriteLock _writeLock = null!;
    private LocalFavoriteService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new SharedMemoryDbConnectionFactory();
        _writeLock = new DbWriteLock();

        using SqliteConnection connection = _factory.Create();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE favoriteLocalTracks (
                sourceId TEXT NOT NULL,
                trackId  TEXT NOT NULL,
                addedAt  TEXT NOT NULL,
                PRIMARY KEY (sourceId, trackId)
            );
            """;
        command.ExecuteNonQuery();

        _service = new LocalFavoriteService(_factory, _writeLock);
    }

    [TearDown]
    public void TearDown()
    {
        _writeLock.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task AddToFavorites_KeepsSourceAndPutsNewestFirst()
    {
        await _service.AddToFavorites(Local);
        await _service.AddToFavorites(Yandex);

        IReadOnlyList<TrackKey> keys = await _service.GetAllFavoriteKeys();

        Assert.Multiple(() =>
        {
            Assert.That(_service.IsTrackFavorite(Local), Is.True);
            Assert.That(_service.IsTrackFavorite(Yandex), Is.True);
            Assert.That(keys, Is.EqualTo(new[] { Yandex, Local }));
            Assert.That(CountRows(), Is.EqualTo(2L));
        });
    }

    [Test]
    public async Task AddToFavorites_IsIdempotent()
    {
        await _service.AddToFavorites(Local);
        await _service.AddToFavorites(Local);

        Assert.That(CountRows(), Is.EqualTo(1L));
    }

    [Test]
    public async Task RemoveFromFavorites_RemovesOnlyThatKey()
    {
        await _service.AddToFavorites(Local);
        await _service.AddToFavorites(Yandex);

        await _service.RemoveFromFavorites(Local);

        Assert.Multiple(() =>
        {
            Assert.That(_service.IsTrackFavorite(Local), Is.False);
            Assert.That(_service.IsTrackFavorite(Yandex), Is.True);
            Assert.That(CountRows(), Is.EqualTo(1L));
        });
    }

    [Test]
    public async Task Constructor_LoadsPersistedFavoritesNewestFirst()
    {
        await Insert("local", "/music/older.mp3", "2026-01-01T00:00:00.0000000Z");
        await Insert("yandex", "999", "2026-01-02T00:00:00.0000000Z");

        var service = new LocalFavoriteService(_factory, _writeLock);

        Assert.That(await service.GetAllFavoriteKeys(), Is.EqualTo(new[]
        {
            new TrackKey(new SourceId("yandex"), "999"),
            new TrackKey(LocalMusicSource.Id, "/music/older.mp3"),
        }));
    }

    [Test]
    public async Task Changed_FiresOnAddAndRemove()
    {
        int changes = 0;
        _service.Changed += () => changes++;

        await _service.AddToFavorites(Local);
        await _service.RemoveFromFavorites(Local);

        Assert.That(changes, Is.EqualTo(2));
    }

    private async Task Insert(string sourceId, string trackId, string addedAt)
    {
        using SqliteConnection connection = _factory.Create();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO favoriteLocalTracks (sourceId, trackId, addedAt) VALUES (@s, @t, @a);";
        command.Parameters.AddWithValue("@s", sourceId);
        command.Parameters.AddWithValue("@t", trackId);
        command.Parameters.AddWithValue("@a", addedAt);
        await command.ExecuteNonQueryAsync();
    }

    private long CountRows()
    {
        using SqliteConnection connection = _factory.Create();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM favoriteLocalTracks;";
        return (long)command.ExecuteScalar()!;
    }
}
