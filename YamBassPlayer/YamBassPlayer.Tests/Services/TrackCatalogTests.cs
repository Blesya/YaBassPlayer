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

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);

    // ── Кэширование одиночного трека ──────────────────────────────────────

    [Test]
    public async Task GetAsync_RepeatedCall_UsesProviderOnce()
    {
        _provider.Setup(p => p.GetTrackInfoById("t1")).ReturnsAsync(Track("t1"));

        Track first = await _catalog.GetAsync("t1");
        Track second = await _catalog.GetAsync("t1");

        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.EqualTo("t1"));
            Assert.That(second, Is.SameAs(first));
            _provider.Verify(p => p.GetTrackInfoById("t1"), Times.Once);
        });
    }

    [Test]
    public async Task Invalidate_DropsCachedEntry_AndRefetches()
    {
        _provider.Setup(p => p.GetTrackInfoById("t1")).ReturnsAsync(Track("t1"));

        await _catalog.GetAsync("t1");
        _catalog.Invalidate("t1");
        await _catalog.GetAsync("t1");

        _provider.Verify(p => p.GetTrackInfoById("t1"), Times.Exactly(2));
    }

    [Test]
    public async Task Clear_DropsAllCachedEntries()
    {
        _provider.Setup(p => p.GetTrackInfoById(It.IsAny<string>()))
            .ReturnsAsync((string id) => Track(id));

        await _catalog.GetAsync("t1");
        await _catalog.GetAsync("t2");
        _catalog.Clear();
        await _catalog.GetAsync("t1");

        Assert.Multiple(() =>
        {
            _provider.Verify(p => p.GetTrackInfoById("t1"), Times.Exactly(2));
            _provider.Verify(p => p.GetTrackInfoById("t2"), Times.Once);
        });
    }

    // ── Пакетная загрузка ─────────────────────────────────────────────────

    [Test]
    public async Task GetManyAsync_PreservesRequestedOrder()
    {
        _provider.Setup(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IEnumerable<string> ids) => ids.Select(id => Track(id)).ToList());

        IReadOnlyList<Track> result = await _catalog.GetManyAsync(["c", "a", "b"]);

        Assert.That(result.Select(t => t.Id), Is.EqualTo(new[] { "c", "a", "b" }));
    }

    [Test]
    public async Task GetManyAsync_CachesFetchedTracks()
    {
        _provider.Setup(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IEnumerable<string> ids) => ids.Select(id => Track(id)).ToList());

        await _catalog.GetManyAsync(["a", "b"]);
        await _catalog.GetAsync("a");

        Assert.Multiple(() =>
        {
            _provider.Verify(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<string>>()), Times.Once);
            _provider.Verify(p => p.GetTrackInfoById(It.IsAny<string>()), Times.Never);
        });
    }

    [Test]
    public async Task GetManyAsync_EmptyInput_DoesNotCallProvider()
    {
        IReadOnlyList<Track> result = await _catalog.GetManyAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            _provider.Verify(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<string>>()), Times.Never);
        });
    }

    // ── Ошибки не кэшируются ──────────────────────────────────────────────

    [Test]
    public void GetAsync_WhenProviderThrows_DoesNotCacheFailure()
    {
        _provider.Setup(p => p.GetTrackInfoById("t1"))
            .ThrowsAsync(new InvalidOperationException("boom"));

        Assert.ThrowsAsync<InvalidOperationException>(() => _catalog.GetAsync("t1"));
        _provider.Verify(p => p.GetTrackInfoById("t1"), Times.Once);

        // После ошибки записи в кэше нет — успешный повтор снова идёт в провайдер.
        _provider.Setup(p => p.GetTrackInfoById("t1")).ReturnsAsync(Track("t1"));
        Track track = _catalog.GetAsync("t1").GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(track.Id, Is.EqualTo("t1"));
            _provider.Verify(p => p.GetTrackInfoById("t1"), Times.Exactly(2));
        });
    }
}
