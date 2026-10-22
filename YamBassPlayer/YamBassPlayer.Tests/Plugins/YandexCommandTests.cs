using Moq;
using YamBassPlayer.Commands;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Plugins;

[TestFixture]
public sealed class YandexCommandTests
{
    private static readonly TrackKey Yandex = new(YandexMusicSourceId.Value, "42");

    [Test]
    public void LikeYandex_NonYandexTrack_Rejects()
    {
        var playback = new Mock<IPlaybackQueue>();
        playback.SetupGet(p => p.CurrentTrack).Returns(new TrackKey(LocalMusicSource.Id, "/a.mp3"));
        var command = new YandexLikeCommand(playback.Object, Mock.Of<IYandexFavoriteService>());

        CommandResult result = command.Execute([]);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void LikeYandex_NotFavorite_Adds()
    {
        var playback = new Mock<IPlaybackQueue>();
        playback.SetupGet(p => p.CurrentTrack).Returns(Yandex);
        var favorites = new Mock<IYandexFavoriteService>();
        favorites.Setup(f => f.IsTrackFavorite("42")).Returns(false);
        favorites.Setup(f => f.AddToFavorites("42")).Returns(Task.CompletedTask);
        var command = new YandexLikeCommand(playback.Object, favorites.Object);

        CommandResult result = command.Execute([]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            favorites.Verify(f => f.AddToFavorites("42"), Times.Once);
        });
    }

    [Test]
    public void SearchCommand_InvokesCallback()
    {
        bool opened = false;
        var command = new YandexSearchCommand(() =>
        {
            opened = true;
            return Task.CompletedTask;
        });

        CommandResult result = command.Execute([]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(opened, Is.True);
        });
    }
}
