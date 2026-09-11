using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class LargeTrackInfoPresenterTests
{
    private Mock<IPlaybackQueue> _queue = null!;
    private Mock<ITrackCatalog> _trackCatalog = null!;
    private Mock<ICoverProvider> _coverProvider = null!;
    private Mock<ICoverArtService> _coverArtService = null!;
    private Mock<IViewFactory> _viewFactory = null!;
    private Mock<ILargeTrackInfoView> _view = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private EventBus _eventBus = null!;
    private LargeTrackInfoPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _queue = new Mock<IPlaybackQueue>();
        _trackCatalog = new Mock<ITrackCatalog>();
        _coverProvider = new Mock<ICoverProvider>();
        _coverArtService = new Mock<ICoverArtService>();
        _viewFactory = new Mock<IViewFactory>();
        _view = new Mock<ILargeTrackInfoView>();
        _errorHandler = new Mock<IErrorHandler>();
        _eventBus = new EventBus();

        _view.SetupGet(v => v.CoverSize).Returns((100, 55));
        _viewFactory.Setup(f => f.Create<ILargeTrackInfoView>()).Returns(_view.Object);

        _presenter = new LargeTrackInfoPresenter(
            _queue.Object,
            _trackCatalog.Object,
            _coverProvider.Object,
            _coverArtService.Object,
            _viewFactory.Object,
            _eventBus,
            _errorHandler.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);

    [Test]
    public void ShowLargeTrackInfo_LoadsPlaylistAndCurrentTrack()
    {
        var tracks = new[] { Track("a"), Track("b") };
        var current = Track("a");
        _queue.SetupGet(q => q.TrackIds).Returns(tracks.Select(t => t.Id).ToList());
        _queue.SetupGet(q => q.CurrentTrackId).Returns("a");
        _trackCatalog.Setup(p => p.GetManyAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(tracks);
        _trackCatalog.Setup(p => p.GetAsync("a")).ReturnsAsync(current);
        _coverProvider.Setup(p => p.DownloadCoverAsync("a")).ReturnsAsync("/covers/a.png");
        _coverArtService
            .Setup(s => s.RenderAsync("/covers/a.png", 100, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoverArt?)null);

        _presenter.ShowLargeTrackInfo();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetPlaylist(It.Is<IReadOnlyList<Track>>(t => t.SequenceEqual(tracks))), Times.Once);
            _view.Verify(v => v.SetCurrentTrackId("a"), Times.Once);
            _view.Verify(v => v.SetTrack(current), Times.Once);
            _view.Verify(v => v.SetCover(null), Times.Once);
            _view.Verify(v => v.Show(), Times.Once);
        });
    }

    [Test]
    public void ShowLargeTrackInfo_WithEmptyQueue_DoesNotLoadPlaylistButStillShows()
    {
        _queue.SetupGet(q => q.TrackIds).Returns([]);
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);

        _presenter.ShowLargeTrackInfo();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetPlaylist(It.IsAny<IReadOnlyList<Track>>()), Times.Never);
            _view.Verify(v => v.Show(), Times.Once);
        });
    }

    [Test]
    public void ShowLargeTrackInfo_WhenTrackInfoFails_ReportsThroughErrorHandler()
    {
        _queue.SetupGet(q => q.TrackIds).Returns([]);
        _queue.SetupGet(q => q.CurrentTrackId).Returns("a");
        _trackCatalog.Setup(p => p.GetAsync("a"))
            .ThrowsAsync(new InvalidOperationException("boom"));

        _presenter.ShowLargeTrackInfo();

        _errorHandler.Verify(h => h.Handle(It.IsAny<InvalidOperationException>()), Times.Once);
    }
}
