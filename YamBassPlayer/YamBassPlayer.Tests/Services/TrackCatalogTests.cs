using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackCatalogTests
{
    private Mock<ITrackInfoProvider> _provider = null!;
    private TrackCatalog _catalog = null!;

    [SetUp]
    public void SetUp()
    {
        _provider = new Mock<ITrackInfoProvider>();
        _catalog = new TrackCatalog(_provider.Object);
    }

    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };

    // ── Кэширование одиночного трека ──────────────────────────────────────

    [Test]
    public async Task GetAsync_RepeatedCall_UsesProviderOnce()
    {
        _provider.Setup(p => p.GetTrackInfoById(K("t1"))).ReturnsAsync(Track("t1"));

        Track first = await _catalog.GetAsync(K("t1"));
        Track second = await _catalog.GetAsync(K("t1"));

        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.EqualTo("t1"));
            Assert.That(second, Is.SameAs(first));
            _provider.Verify(p => p.GetTrackInfoById(K("t1")), Times.Once);
        });
    }

    [Test]
    public async Task Invalidate_DropsCachedEntry_AndRefetches()
    {
        _provider.Setup(p => p.GetTrackInfoById(K("t1"))).ReturnsAsync(Track("t1"));

        await _catalog.GetAsync(K("t1"));
        _catalog.Invalidate(K("t1"));
        await _catalog.GetAsync(K("t1"));

        _provider.Verify(p => p.GetTrackInfoById(K("t1")), Times.Exactly(2));
    }

    [Test]
    public async Task Clear_DropsAllCachedEntries()
    {
        _provider.Setup(p => p.GetTrackInfoById(It.IsAny<TrackKey>()))
            .ReturnsAsync((TrackKey key) => Track(key.TrackId));

        await _catalog.GetAsync(K("t1"));
        await _catalog.GetAsync(K("t2"));
        _catalog.Clear();
        await _catalog.GetAsync(K("t1"));

        Assert.Multiple(() =>
        {
            _provider.Verify(p => p.GetTrackInfoById(K("t1")), Times.Exactly(2));
            _provider.Verify(p => p.GetTrackInfoById(K("t2")), Times.Once);
        });
    }

    // ── Пакетная загрузка ─────────────────────────────────────────────────

    [Test]
    public async Task GetManyAsync_PreservesRequestedOrder()
    {
        _provider.Setup(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<TrackKey>>()))
            .ReturnsAsync((IEnumerable<TrackKey> keys) => keys.Select(key => Track(key.TrackId)).ToList());

        IReadOnlyList<Track> result = await _catalog.GetManyAsync([K("c"), K("a"), K("b")]);

        Assert.That(result.Select(t => t.Id), Is.EqualTo(new[] { "c", "a", "b" }));
    }

    [Test]
    public async Task GetManyAsync_CachesFetchedTracks()
    {
        _provider.Setup(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<TrackKey>>()))
            .ReturnsAsync((IEnumerable<TrackKey> keys) => keys.Select(key => Track(key.TrackId)).ToList());

        await _catalog.GetManyAsync([K("a"), K("b")]);
        await _catalog.GetAsync(K("a"));

        Assert.Multiple(() =>
        {
            _provider.Verify(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<TrackKey>>()), Times.Once);
            _provider.Verify(p => p.GetTrackInfoById(It.IsAny<TrackKey>()), Times.Never);
        });
    }

    [Test]
    public async Task GetManyAsync_EmptyInput_DoesNotCallProvider()
    {
        IReadOnlyList<Track> result = await _catalog.GetManyAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _provider.Verify(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<TrackKey>>()), Times.Never);
        });
    }

    // ── Ошибки не кэшируются ──────────────────────────────────────────────

    [Test]
    public void GetAsync_WhenProviderThrows_DoesNotCacheFailure()
    {
        _provider.Setup(p => p.GetTrackInfoById(K("t1")))
            .ThrowsAsync(new InvalidOperationException("boom"));

        Assert.ThrowsAsync<InvalidOperationException>(() => _catalog.GetAsync(K("t1")));
        _provider.Verify(p => p.GetTrackInfoById(K("t1")), Times.Once);

        // После ошибки записи в кэше нет — успешный повтор снова идёт в провайдер.
        _provider.Setup(p => p.GetTrackInfoById(K("t1"))).ReturnsAsync(Track("t1"));
        Track track = _catalog.GetAsync(K("t1")).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(track.Id, Is.EqualTo("t1"));
            _provider.Verify(p => p.GetTrackInfoById(K("t1")), Times.Exactly(2));
        });
    }
}
