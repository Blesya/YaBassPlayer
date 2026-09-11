using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class PlayStatusPresenterTests
{
    private Mock<IPlayStatusView> _view = null!;
    private Mock<IAudioPlayer> _audioPlayer = null!;
    private Mock<ITrackFavoriteService> _favorites = null!;

    [SetUp]
    public void SetUp()
    {
        _view = new Mock<IPlayStatusView>();
        _audioPlayer = new Mock<IAudioPlayer>();
        _favorites = new Mock<ITrackFavoriteService>();
    }

    private PlayStatusPresenter CreatePresenter()
        => new(_view.Object, _audioPlayer.Object, _favorites.Object);

    // ── Проброс событий вью → презентер ───────────────────────────────────

    [Test]
    public void ViewPlayClicked_RaisesPresenterEvent()
    {
        var presenter = CreatePresenter();
        bool raised = false;
        presenter.OnPlayClicked += () => raised = true;

        _view.Raise(v => v.OnPlayClicked += null);

        Assert.That(raised, Is.True);
    }

    [Test]
    public void ViewSeekRequested_SeeksAudioToPercent()
    {
        _ = CreatePresenter();

        _view.Raise(v => v.OnSeekRequested += null, 42);

        _audioPlayer.Verify(a => a.SeekToPercent(42), Times.Once);
    }

    [Test]
    public void ViewQueueModeAndRestartEvents_AreBubbled()
    {
        var presenter = CreatePresenter();
        bool queue = false, mode = false, restart = false;
        presenter.OnQueueClicked += () => queue = true;
        presenter.OnPlaybackModeToggled += () => mode = true;
        presenter.OnRestartClicked += () => restart = true;

        _view.Raise(v => v.OnQueueClicked += null);
        _view.Raise(v => v.OnPlaybackModeToggled += null);
        _view.Raise(v => v.OnRestartClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.True);
            Assert.That(mode, Is.True);
            Assert.That(restart, Is.True);
        });
    }

    // ── Избранное: видимость/доступность ──────────────────────────────────

    [Test]
    public void Constructor_SetsFavoriteVisibilityFromSupportedSources()
    {
        _favorites.Setup(f => f.SupportsSource(SourceIds.Local)).Returns(true);

        _ = CreatePresenter();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetLocalFavoriteVisibility(true), Times.AtLeastOnce);
            _view.Verify(v => v.SetLocalFavoriteEnabled(false), Times.AtLeastOnce); // нет текущего трека
        });
    }

    [Test]
    public void SetCurrentTrack_LocalTrack_EnablesLocalFavorite()
    {
        _favorites.Setup(f => f.SupportsSource(SourceIds.Local)).Returns(true);
        var presenter = CreatePresenter();

        presenter.SetCurrentTrack("/music/a.mp3", SourceIds.Local);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetLocalFavoriteVisibility(true), Times.AtLeastOnce);
            _view.Verify(v => v.SetLocalFavoriteEnabled(true), Times.AtLeastOnce);
        });
    }

    [Test]
    public void SetCurrentTrack_YandexTrack_EnablesYandexFavorite()
    {
        _favorites.Setup(f => f.SupportsSource(SourceIds.Yandex)).Returns(true);
        var presenter = CreatePresenter();

        presenter.SetCurrentTrack("12345", SourceIds.Yandex);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetYandexFavoriteVisibility(true), Times.AtLeastOnce);
            _view.Verify(v => v.SetYandexFavoriteEnabled(true), Times.AtLeastOnce);
        });
    }

    [Test]
    public void SetCurrentTrack_YandexTrack_DoesNotEnableLocalFavorite()
    {
        _favorites.Setup(f => f.SupportsSource(SourceIds.Local)).Returns(true);
        var presenter = CreatePresenter();

        presenter.SetCurrentTrack("12345", SourceIds.Yandex);

        _view.Verify(v => v.SetLocalFavoriteEnabled(false), Times.AtLeastOnce);
    }

    // ── Переключение избранного ───────────────────────────────────────────

    [Test]
    public void ToggleLocalFavorite_WhenNotFavorite_AddsToFavorites()
    {
        _favorites.Setup(f => f.SupportsSource(SourceIds.Local)).Returns(true);
        _favorites.Setup(f => f.IsTrackFavorite(SourceIds.Local, "/music/a.mp3")).Returns(false);
        var presenter = CreatePresenter();
        presenter.SetCurrentTrack("/music/a.mp3", SourceIds.Local);

        _view.Raise(v => v.OnLocalFavoriteToggleClicked += null);

        _favorites.Verify(f => f.AddToFavorites(SourceIds.Local, "/music/a.mp3"), Times.Once);
    }

    [Test]
    public void ToggleLocalFavorite_WhenFavorite_RemovesFromFavorites()
    {
        _favorites.Setup(f => f.SupportsSource(SourceIds.Local)).Returns(true);
        _favorites.Setup(f => f.IsTrackFavorite(SourceIds.Local, "/music/a.mp3")).Returns(true);
        var presenter = CreatePresenter();
        presenter.SetCurrentTrack("/music/a.mp3", SourceIds.Local);

        _view.Raise(v => v.OnLocalFavoriteToggleClicked += null);

        _favorites.Verify(f => f.RemoveFromFavorites(SourceIds.Local, "/music/a.mp3"), Times.Once);
    }

    [Test]
    public void ToggleFavorite_WithNoCurrentTrack_DoesNothing()
    {
        _favorites.Setup(f => f.SupportsSource(SourceIds.Local)).Returns(true);
        _ = CreatePresenter();

        _view.Raise(v => v.OnLocalFavoriteToggleClicked += null);

        _favorites.Verify(f => f.AddToFavorites(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── Прочие делегирования ──────────────────────────────────────────────

    [Test]
    public void SetPlaybackMode_DelegatesToView()
    {
        var presenter = CreatePresenter();

        presenter.SetPlaybackMode(PlaybackMode.Shuffle);

        _view.Verify(v => v.SetPlaybackMode(PlaybackMode.Shuffle), Times.Once);
    }

    [Test]
    public void SetPlayStatusAndTitle_DelegateToView()
    {
        var presenter = CreatePresenter();

        presenter.SetPlayStatus("статус");
        presenter.SetTitle("заголовок");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetPlayStatus("статус"), Times.Once);
            _view.Verify(v => v.SetTitle("заголовок"), Times.Once);
        });
    }
}
