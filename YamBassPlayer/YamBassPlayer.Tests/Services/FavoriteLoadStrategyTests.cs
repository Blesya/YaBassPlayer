using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

/// <summary>
/// Плейлист «Локальное избранное» строится из локально сохранённых ключей,
/// а не из лайков Яндекс.Музыки.
/// </summary>
[TestFixture]
public sealed class FavoriteLoadStrategyTests
{
    [Test]
    public async Task FavoriteLoadStrategy_ReturnsLocalFavoriteKeys()
    {
        var local = new TrackKey(new SourceId("local"), "/music/a.mp3");
        var yandex = new TrackKey(new SourceId("yandex"), "12345");

        var favorites = new Mock<ILocalFavoriteService>();
        favorites.Setup(f => f.GetAllFavoriteKeys()).ReturnsAsync([yandex, local]);

        List<TrackKey> keys = await new FavoriteLoadStrategy(favorites.Object)
            .LoadTrackIdsAsync(new Playlist("favorite", "Локальное избранное"));

        Assert.That(keys, Is.EqualTo(new[] { yandex, local }));
    }
}
