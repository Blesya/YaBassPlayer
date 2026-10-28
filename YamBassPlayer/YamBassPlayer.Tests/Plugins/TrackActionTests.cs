using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Plugins;

/// <summary>
/// Кнопки лайков в панели: закрашенное сердце — трек уже в избранном,
/// контурное — ещё нет. Нажатие в обоих состояниях переключает признак.
/// </summary>
[TestFixture]
public sealed class TrackActionTests
{
    private static readonly Track LocalTrack = new("Название", "Исполнитель", "Альбом", "/music/a.mp3")
    {
        SourceId = LocalMusicSource.Id
    };

    private static readonly Track YandexTrack = new("Название", "Исполнитель", "Альбом", "42")
    {
        SourceId = YandexMusicSourceId.Value
    };

    [Test]
    public void LocalAction_TitleShowsHeartState()
    {
        var favorites = new Mock<ILocalFavoriteService>();
        favorites.Setup(f => f.IsTrackFavorite(LocalTrack.Key)).Returns(false);
        var action = new FavoriteTrackAction(favorites.Object);

        Assert.That(action.GetTitle(LocalTrack), Is.EqualTo("Локально ♡"));

        favorites.Setup(f => f.IsTrackFavorite(LocalTrack.Key)).Returns(true);
        Assert.That(action.GetTitle(LocalTrack), Is.EqualTo("Локально ♥"));
    }

    [Test]
    public void LocalAction_ClickTogglesFavoriteAndNotifies()
    {
        var favorites = new Mock<ILocalFavoriteService>();
        var action = new FavoriteTrackAction(favorites.Object);
        int changed = 0;
        action.Changed += () => changed++;

        favorites.Setup(f => f.IsTrackFavorite(LocalTrack.Key)).Returns(false);
        action.ExecuteAsync(LocalTrack).GetAwaiter().GetResult();
        favorites.Verify(f => f.AddToFavorites(LocalTrack.Key), Times.Once);

        favorites.Setup(f => f.IsTrackFavorite(LocalTrack.Key)).Returns(true);
        action.ExecuteAsync(LocalTrack).GetAwaiter().GetResult();
        favorites.Verify(f => f.RemoveFromFavorites(LocalTrack.Key), Times.Once);

        Assert.That(changed, Is.EqualTo(2));
    }

    [Test]
    public void YandexAction_TitleShowsHeartState()
    {
        var favorites = new Mock<IYandexFavoriteService>();
        favorites.Setup(f => f.IsTrackFavorite("42")).Returns(false);
        var action = new YandexLikeTrackAction(favorites.Object);

        Assert.Multiple(() =>
        {
            Assert.That(action.IsAvailable(YandexTrack), Is.True);
            Assert.That(action.IsAvailable(LocalTrack), Is.False);
            Assert.That(action.GetTitle(YandexTrack), Is.EqualTo("Яндекс ♡"));
        });

        favorites.Setup(f => f.IsTrackFavorite("42")).Returns(true);
        Assert.That(action.GetTitle(YandexTrack), Is.EqualTo("Яндекс ♥"));
    }

    [Test]
    public void YandexAction_ClickTogglesLikeAndNotifies()
    {
        var favorites = new Mock<IYandexFavoriteService>();
        var action = new YandexLikeTrackAction(favorites.Object);
        int changed = 0;
        action.Changed += () => changed++;

        favorites.Setup(f => f.IsTrackFavorite("42")).Returns(false);
        action.ExecuteAsync(YandexTrack).GetAwaiter().GetResult();
        favorites.Verify(f => f.AddToFavorites("42"), Times.Once);

        favorites.Setup(f => f.IsTrackFavorite("42")).Returns(true);
        action.ExecuteAsync(YandexTrack).GetAwaiter().GetResult();
        favorites.Verify(f => f.RemoveFromFavorites("42"), Times.Once);

        Assert.That(changed, Is.EqualTo(2));
    }
}
