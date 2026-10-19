using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class YandexPlaylistLoadStrategyTests
{
    private static Playlist OwnPlaylist() =>
        new("Мой микс", PlaylistType.YandexOwnPlaylist)
        {
            Payload = new PlaylistPayload { YandexOwnerUid = "42", YandexKind = "7" }
        };

    [TestCase(PlaylistType.YandexOwnPlaylist, true)]
    [TestCase(PlaylistType.YandexLikedPlaylist, true)]
    [TestCase(PlaylistType.Custom, false)]
    [TestCase(PlaylistType.Favorite, false)]
    public void CanHandle_OnlyYandexPlaylistTypes(PlaylistType type, bool expected)
    {
        var strategy = new YandexPlaylistLoadStrategy(
            new Mock<IMusicSourceRegistry>().Object,
            new TrackRepositoryCache());

        Assert.That(strategy.CanHandle(type), Is.EqualTo(expected));
    }

    [Test]
    public async Task LoadTrackIds_FetchesAndCachesUnderOwnerAndKind()
    {
        var playlist = OwnPlaylist();
        var source = new Mock<IMusicSource>();
        source
            .Setup(s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "t1", "t2" });

        var registry = new Mock<IMusicSourceRegistry>();
        registry.Setup(r => r.GetRequired(SourceIds.Yandex)).Returns(source.Object);

        var cache = new TrackRepositoryCache();
        var strategy = new YandexPlaylistLoadStrategy(registry.Object, cache);

        var trackIds = await strategy.LoadTrackIdsAsync(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(trackIds, Is.EqualTo(new[] { "t1", "t2" }));
            Assert.That(cache.TryGetCustomPlaylistIds("42:7", out var cached), Is.True);
            Assert.That(cached, Is.EqualTo(new[] { "t1", "t2" }));
        });
    }

    [Test]
    public async Task LoadTrackIds_UsesCachedIdsWithoutRequest()
    {
        var playlist = OwnPlaylist();
        var source = new Mock<IMusicSource>();
        var registry = new Mock<IMusicSourceRegistry>();
        registry.Setup(r => r.GetRequired(SourceIds.Yandex)).Returns(source.Object);

        var cache = new TrackRepositoryCache();
        cache.SetCustomPlaylistIds("42:7", ["cached"]);

        var strategy = new YandexPlaylistLoadStrategy(registry.Object, cache);
        var trackIds = await strategy.LoadTrackIdsAsync(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(trackIds, Is.EqualTo(new[] { "cached" }));
            source.Verify(
                s => s.GetPlaylistTrackIdsAsync(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()),
                Times.Never);
        });
    }
}
