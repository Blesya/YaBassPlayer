using Microsoft.Data.Sqlite;
using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Tests.Data;

namespace YamBassPlayer.Tests.Services;

/// <summary>
/// Ядро спрашивает подробности о треке и текст песни у источника через
/// необязательные возможности, а не у конкретной реализации.
/// </summary>
[TestFixture]
public sealed class TrackDetailsDelegationTests
{
    private static readonly SourceId TestSource = new("test-source");
    private static readonly TrackKey Key = new(TestSource, "track-1");

    [Test]
    public async Task TrackAbout_DelegatesToSourceCapability()
    {
        var info = new TrackAboutInfo { TrackId = "track-1", Title = "Из источника", Artist = "Исполнитель" };
        var source = new Mock<IMusicSource>();
        source.As<ITrackAboutSource>()
            .Setup(s => s.GetAboutAsync(Key, true))
            .ReturnsAsync(info);

        var service = new TrackAboutService(Registry(source.Object), Mock.Of<ITrackCatalog>());

        TrackAboutInfo result = await service.GetAsync(Key, forceRefresh: true);

        Assert.That(result, Is.SameAs(info));
    }

    [Test]
    public async Task TrackAbout_FallsBackToCatalog_WhenSourceHasNoCapability()
    {
        var source = new Mock<IMusicSource>();
        var catalog = new Mock<ITrackCatalog>();
        catalog.Setup(c => c.GetAsync(Key))
            .ReturnsAsync(new Track("Название", "Исполнитель", "Альбом", "track-1") { SourceId = TestSource });

        var service = new TrackAboutService(Registry(source.Object), catalog.Object);

        TrackAboutInfo result = await service.GetAsync(Key);

        Assert.That(result.Title, Is.EqualTo("Название"));
        Assert.That(result.Artist, Is.EqualTo("Исполнитель"));
        Assert.That(result.Album, Is.EqualTo("Альбом"));
    }

    [Test]
    public async Task Lyrics_DelegatesToSourceAndCachesResult()
    {
        using var factory = new SharedMemoryDbConnectionFactory();
        using (SqliteConnection connection = factory.Create())
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE Tracks (TrackId TEXT PRIMARY KEY, Lyrics TEXT);
                INSERT INTO Tracks (TrackId, Lyrics) VALUES ('track-1', NULL);
                """;
            command.ExecuteNonQuery();
        }

        var track = new Track("Название", "Исполнитель", "Альбом", "track-1") { SourceId = TestSource };
        var source = new Mock<IMusicSource>();
        source.As<ILyricsSource>()
            .Setup(s => s.GetLyricsAsync(track))
            .ReturnsAsync("текст песни");

        var service = new LyricsService(Registry(source.Object), factory, new DbWriteLock());

        string? first = await service.GetLyricsAsync(track);
        string? second = await service.GetLyricsAsync(track);

        Assert.That(first, Is.EqualTo("текст песни"));
        Assert.That(second, Is.EqualTo("текст песни"));
        source.As<ILyricsSource>().Verify(s => s.GetLyricsAsync(track), Times.Once);
    }

    [Test]
    public async Task Lyrics_ReturnsNull_WhenSourceHasNoCapability()
    {
        using var factory = new SharedMemoryDbConnectionFactory();
        using (SqliteConnection connection = factory.Create())
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE Tracks (TrackId TEXT PRIMARY KEY, Lyrics TEXT);
                INSERT INTO Tracks (TrackId, Lyrics) VALUES ('track-1', NULL);
                """;
            command.ExecuteNonQuery();
        }

        var track = new Track("Название", "Исполнитель", "Альбом", "track-1") { SourceId = TestSource };

        var service = new LyricsService(Registry(new Mock<IMusicSource>().Object), factory, new DbWriteLock());

        Assert.That(await service.GetLyricsAsync(track), Is.Null);
    }

    private static IMusicSourceRegistry Registry(IMusicSource source)
    {
        var registry = new Mock<IMusicSourceRegistry>();
        registry.Setup(r => r.Get(TestSource)).Returns(source);
        return registry.Object;
    }
}
