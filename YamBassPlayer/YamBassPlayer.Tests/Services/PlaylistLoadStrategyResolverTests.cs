namespace YamBassPlayer.Tests.Services;

using Moq;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class PlaylistLoadStrategyResolverTests
{
    private static IPlaylistLoadStrategy CreateStrategy(string kind)
    {
        var mock = new Mock<IPlaylistLoadStrategy>();
        mock.SetupGet(s => s.Kind).Returns(kind);
        return mock.Object;
    }

    [Test]
    public void Resolve_ReturnsCorrectStrategy_ForHandledKind()
    {
        var top10 = CreateStrategy("top10");
        var cached = CreateStrategy("cached");

        var resolver = new PlaylistLoadStrategyResolver(new[] { top10, cached });

        Assert.Multiple(() =>
        {
            Assert.That(resolver.Resolve("top10"), Is.SameAs(top10));
            Assert.That(resolver.Resolve("cached"), Is.SameAs(cached));
        });
    }

    [Test]
    public void Resolve_FirstRegisteredStrategyWins_WhenMultipleMatchSameKind()
    {
        var first = CreateStrategy("top10");
        var second = CreateStrategy("top10");

        var resolver = new PlaylistLoadStrategyResolver(new IPlaylistLoadStrategy[] { first, second });

        Assert.That(resolver.Resolve("top10"), Is.SameAs(first));
    }

    [Test]
    public void Resolve_ThrowsInvalidOperationException_WhenNoStrategyForKind()
    {
        var strategy = CreateStrategy("top10");
        var resolver = new PlaylistLoadStrategyResolver(new[] { strategy });

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("my-wave"));
        Assert.That(ex!.Message, Does.Contain("my-wave"));
    }

    [Test]
    public void Resolve_ThrowsInvalidOperationException_WhenNoStrategiesRegistered()
    {
        var resolver = new PlaylistLoadStrategyResolver(Enumerable.Empty<IPlaylistLoadStrategy>());

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("queue"));
        Assert.That(ex!.Message, Does.Contain("queue"));
    }

    [Test]
    public void EveryCoreKindHasStrategy()
    {
        var cache = new Mock<ITrackRepositoryCache>();
        cache.SetupGet(c => c.QueueTrackIds).Returns([]);
        var history = new Mock<IHistoryService>();
        var trackInfo = new Mock<ITrackInfoProvider>();
        string tracksFolder = Path.Combine(Path.GetTempPath(), $"tracks_{Guid.NewGuid():N}");

        var strategies = new IPlaylistLoadStrategy[]
        {
            new QueueLoadStrategy(cache.Object),
            new CachedLoadStrategy(tracksFolder),
            new Top10LoadStrategy(history.Object),
            new TopEveningsLoadStrategy(history.Object),
            new TopByDayLoadStrategy(history.Object),
            new ArtistLoadStrategy(trackInfo.Object)
        };

        var resolver = new PlaylistLoadStrategyResolver(strategies);

        string[] coreKinds = ["queue", "cached", "top10", "top-evenings", "top-by-day", "artist"];
        Assert.Multiple(() =>
        {
            Assert.That(strategies.Select(s => s.Kind), Is.EquivalentTo(coreKinds));
            foreach (string kind in coreKinds)
                Assert.That(resolver.Resolve(kind), Is.SameAs(strategies.Single(s => s.Kind == kind)));
        });
    }
}
