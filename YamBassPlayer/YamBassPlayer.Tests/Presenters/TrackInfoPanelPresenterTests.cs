using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class TrackInfoPanelPresenterTests
{
    private Mock<ITrackInfoPanelView> _view = null!;
    private Mock<ICoverProvider> _coverProvider = null!;
    private Mock<ICoverArtService> _coverArtService = null!;
    private Mock<ILyricsService> _lyricsService = null!;
    private Mock<ITrackCatalog> _trackCatalog = null!;
    private Mock<IPlaybackQueue> _queue = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private EventBus _eventBus = null!;

    [SetUp]
    public void SetUp()
    {
        _view = new Mock<ITrackInfoPanelView>();
        _coverProvider = new Mock<ICoverProvider>();
        _coverArtService = new Mock<ICoverArtService>();
        _lyricsService = new Mock<ILyricsService>();
        _trackCatalog = new Mock<ITrackCatalog>();
        _queue = new Mock<IPlaybackQueue>();
        _errorHandler = new Mock<IErrorHandler>();
        _eventBus = new EventBus();

        _view.SetupGet(v => v.CoverSize).Returns((34, 17));
    }

    private TrackInfoPanelPresenter CreatePresenter() => new(
        _view.Object,
        _coverProvider.Object,
        _coverArtService.Object,
        _lyricsService.Object,
        _trackCatalog.Object,
        _queue.Object,
        _eventBus,
        _errorHandler.Object);

    private Track SetupTrack(string id)
    {
        var track = new Track($"Title-{id}", $"Artist-{id}", "Album", id);
        _trackCatalog.Setup(p => p.GetAsync(id)).ReturnsAsync(track);
        _coverProvider.Setup(p => p.DownloadCoverAsync(id)).ReturnsAsync($"/covers/{id}.png");
        _lyricsService.Setup(s => s.GetLyricsAsync(track)).ReturnsAsync($"lyrics-{id}");
        return track;
    }

    // ── Загрузка при создании (если очередь уже играет) ───────────────────

    [Test]
    public void Constructor_WhenQueueHasCurrentTrack_LoadsTrackCoverAndLyrics()
    {
        var track = SetupTrack("t1");
        _queue.SetupGet(q => q.CurrentTrackId).Returns("t1");
        _coverArtService
            .Setup(s => s.RenderAsync("/covers/t1.png", 34, 17, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoverArt?)null);

        _ = CreatePresenter();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetTrack(track), Times.Once);
            _coverArtService.Verify(s => s.RenderAsync("/covers/t1.png", 34, 17, It.IsAny<CancellationToken>()), Times.Once);
            _view.Verify(v => v.SetCover(null), Times.Once);
            _view.Verify(v => v.SetLyrics("lyrics-t1"), Times.Once);
        });
    }

    [Test]
    public void Constructor_WhenQueueIsEmpty_LoadsNothing()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);

        _ = CreatePresenter();

        _view.Verify(v => v.SetTrack(It.IsAny<Track>()), Times.Never);
    }

    // ── Реакция на смену трека ────────────────────────────────────────────

    [Test]
    public void TrackChangedEvent_LoadsNewTrack()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);
        var track = SetupTrack("t2");
        _coverArtService
            .Setup(s => s.RenderAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoverArt?)null);
        _ = CreatePresenter();

        _eventBus.Publish(new TrackChangedEvent("t2"));

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetTrack(track), Times.Once);
            _view.Verify(v => v.SetLyrics("lyrics-t2"), Times.Once);
        });
    }

    [Test]
    public void TrackChangedEvent_WithSameTrackTwice_LoadsOnlyOnce()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);
        SetupTrack("t3");
        _coverArtService
            .Setup(s => s.RenderAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoverArt?)null);
        _ = CreatePresenter();

        _eventBus.Publish(new TrackChangedEvent("t3"));
        _eventBus.Publish(new TrackChangedEvent("t3"));

        _trackCatalog.Verify(p => p.GetAsync("t3"), Times.Once);
    }

    [Test]
    public void TrackChangedEvent_WhenProviderThrows_HandlesErrorAndClearsLyrics()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);
        var ex = new InvalidOperationException("boom");
        _trackCatalog.Setup(p => p.GetAsync("t4")).ThrowsAsync(ex);
        _ = CreatePresenter();

        _eventBus.Publish(new TrackChangedEvent("t4"));

        Assert.Multiple(() =>
        {
            _errorHandler.Verify(e => e.Handle(ex), Times.Once);
            _view.Verify(v => v.SetLyrics(null), Times.Once);
        });
    }
}
