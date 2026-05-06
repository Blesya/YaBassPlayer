namespace YamBassPlayer.Tests.Services;

using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class PlaylistLoadStrategyResolverTests
{
    private static Mock<IPlaylistLoadStrategy> CreateStrategyMock(
        string name, params PlaylistType[] handledTypes)
    {
        var mock = new Mock<IPlaylistLoadStrategy>();
        mock.Setup(s => s.CanHandle(It.IsAny<PlaylistType>()))
            .Returns<PlaylistType>(t => handledTypes.Contains(t));
        return mock;
    }

    // ──────────────── Maps type to correct strategy ────────────────

    [Test]
    public void Resolve_ReturnsCorrectStrategy_ForHandledPlaylistType()
    {
        var favoriteStrategy = CreateStrategyMock("favorite", PlaylistType.Favorite).Object;
        var searchStrategy = CreateStrategyMock("search", PlaylistType.YandexSearch).Object;

        var resolver = new PlaylistLoadStrategyResolver(new[] { favoriteStrategy, searchStrategy });

        Assert.Multiple(() =>
        {
            Assert.That(resolver.Resolve(PlaylistType.Favorite), Is.SameAs(favoriteStrategy));
            Assert.That(resolver.Resolve(PlaylistType.YandexSearch), Is.SameAs(searchStrategy));
        });
    }

    // ──────────────── First registered strategy wins ────────────────

    [Test]
    public void Resolve_FirstRegisteredStrategyWins_WhenMultipleMatchSameType()
    {
        var first = CreateStrategyMock("first", PlaylistType.Favorite).Object;
        var second = CreateStrategyMock("second", PlaylistType.Favorite).Object;

        var resolver = new PlaylistLoadStrategyResolver(new IPlaylistLoadStrategy[] { first, second });

        Assert.That(resolver.Resolve(PlaylistType.Favorite), Is.SameAs(first));
    }

    // ──────────────── No strategy for type ────────────────

    [Test]
    public void Resolve_ThrowsInvalidOperationException_WhenNoStrategyForType()
    {
        var strategy = CreateStrategyMock("only", PlaylistType.Favorite).Object;
        var resolver = new PlaylistLoadStrategyResolver(new[] { strategy });

        var ex = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve(PlaylistType.MyWave));
        Assert.That(ex!.Message, Does.Contain("PlaylistType.MyWave"));
    }

    // ──────────────── Empty strategy collection ────────────────

    [Test]
    public void Resolve_ThrowsInvalidOperationException_WhenNoStrategiesRegistered()
    {
        var resolver = new PlaylistLoadStrategyResolver(Enumerable.Empty<IPlaylistLoadStrategy>());

        foreach (PlaylistType type in Enum.GetValues<PlaylistType>())
        {
            var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(type));
            Assert.That(ex!.Message, Does.Contain(type.ToString()));
        }
    }
}
