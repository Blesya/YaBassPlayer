using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Tests.Data;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Tests.Services;

/// <summary>
/// Интеграционные тесты кэша треков на реальном SQLite (in-memory):
/// покрывают извлечённые <c>TrackCacheWriter</c>, <c>TrackEnricher</c>, <c>TrackRowMapper</c>.
/// </summary>
[TestFixture]
public sealed class TrackInfoProviderTests
{
    private SharedMemoryDbConnectionFactory _factory = null!;
    private TrackInfoProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new SharedMemoryDbConnectionFactory();
        new DatabaseInitializer(_factory).Initialize();

        var registry = new Mock<IMusicSourceRegistry>();
        _provider = new TrackInfoProvider(
            new YandexMusicApi(),
            new AuthStorage(),
            _factory,
            new DbWriteLock(),
            registry.Object);
    }

    [TearDown]
    public void TearDown() => _factory?.Dispose();

    private static Track Track(string id, string artist = "Артист", string title = "Трек", string album = "Альбом")
        => new(title, artist, album, id);

    [Test]
    public async Task SaveAsync_ThenGetTrackInfoById_RoundTrips()
    {
        await _provider.SaveAsync(Track("t1"));

        Track loaded = await _provider.GetTrackInfoById("t1");

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Id, Is.EqualTo("t1"));
            Assert.That(loaded.Artist, Is.EqualTo("Артист"));
            Assert.That(loaded.Title, Is.EqualTo("Трек"));
            Assert.That(loaded.Album, Is.EqualTo("Альбом"));
        });
    }

    [Test]
    public async Task SaveAsync_PersistsGenresArtistsAndAlbum()
    {
        var track = new Track("Заголовок", "Исполнитель", "Альбом", "t2")
        {
            Genres = new List<string> { "rock", "indie" },
            AlbumInfo = new Album("alb1", "Альбом") { Year = 2020, TrackCount = 10 },
            Artists = new List<Artist> { new("ar1", "Исполнитель") },
        };

        await _provider.SaveAsync(track);
        Track loaded = await _provider.GetTrackInfoById("t2");

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Genres, Is.EquivalentTo(new[] { "rock", "indie" }));
            Assert.That(loaded.AlbumInfo?.Id, Is.EqualTo("alb1"));
            Assert.That(loaded.AlbumInfo?.Year, Is.EqualTo(2020));
            Assert.That(loaded.Artists, Has.Count.EqualTo(1));
            Assert.That(loaded.Artists![0].Id, Is.EqualTo("ar1"));
        });
    }

    [Test]
    public async Task GetTracksInfoByIds_RestoresRequestedOrder()
    {
        await _provider.SaveAsync(Track("a", title: "A"));
        await _provider.SaveAsync(Track("b", title: "B"));
        await _provider.SaveAsync(Track("c", title: "C"));

        var result = (await _provider.GetTracksInfoByIds(["c", "a", "b"])).ToList();

        Assert.That(result.Select(t => t.Id), Is.EqualTo(new[] { "c", "a", "b" }));
    }

    [Test]
    public async Task GetTracksInfoByIds_SkipsUnknownIds()
    {
        await _provider.SaveAsync(Track("a"));
        var registry = new Mock<IMusicSourceRegistry>();
        var provider = new TrackInfoProvider(
            new YandexMusicApi(), new AuthStorage(), _factory, new DbWriteLock(), registry.Object);

        var result = (await provider.GetTracksInfoByIds(["a"])).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IsTrackCached_ReflectsSave()
    {
        Assert.That(await _provider.IsTrackCached("x"), Is.False);

        await _provider.SaveAsync(Track("x"));

        Assert.That(await _provider.IsTrackCached("x"), Is.True);
    }

    [Test]
    public async Task CountCachedTracks_StopsAtFirstMiss()
    {
        await _provider.SaveAsync(Track("a"));
        await _provider.SaveAsync(Track("b"));

        int count = await _provider.CountCachedTracks(["a", "b", "c"]);

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task SearchTracks_MatchesTitleArtistOrAlbum()
    {
        await _provider.SaveAsync(Track("s1", artist: "Кино", title: "Песня"));
        await _provider.SaveAsync(Track("s2", artist: "Queen", title: "Song"));

        var byArtist = (await _provider.SearchTracks("Кино")).ToList();
        var byTitle = (await _provider.SearchTracks("Song")).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(byArtist.Select(t => t.Id), Is.EqualTo(new[] { "s1" }));
            Assert.That(byTitle.Select(t => t.Id), Is.EqualTo(new[] { "s2" }));
        });
    }

    [Test]
    public async Task GetArtistsWithTrackCountAsync_GroupsByArtist()
    {
        await _provider.SaveAsync(Track("a1", artist: "A"));
        await _provider.SaveAsync(Track("a2", artist: "A"));
        await _provider.SaveAsync(Track("b1", artist: "B"));

        var result = await _provider.GetArtistsWithTrackCountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain(("A", 2)));
            Assert.That(result, Does.Contain(("B", 1)));
        });
    }

    [Test]
    public async Task GetTrackIdsByArtistAsync_ReturnsArtistTracks()
    {
        await _provider.SaveAsync(Track("a1", artist: "A"));
        await _provider.SaveAsync(Track("a2", artist: "A"));
        await _provider.SaveAsync(Track("b1", artist: "B"));

        var result = await _provider.GetTrackIdsByArtistAsync("A");

        Assert.That(result, Is.EquivalentTo(new[] { "a1", "a2" }));
    }
}
