using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class MyWaveWindowPresenterTests
{
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private const int CoverWidth = 100;
    private const int CoverHeight = 55;

    private Mock<IPlaybackQueue> _queue = null!;
    private Mock<ITrackCatalog> _trackCatalog = null!;
    private Mock<ICoverProvider> _coverProvider = null!;
    private Mock<ICoverArtService> _coverArtService = null!;
    private Mock<IViewFactory> _viewFactory = null!;
    private Mock<IModalWindowHost> _host = null!;
    private Mock<IMyWaveView> _view = null!;
    private FakeUiDispatcher _ui = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private EventBus _eventBus = null!;
    private MyWaveWindowPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _queue = new Mock<IPlaybackQueue>();
        _trackCatalog = new Mock<ITrackCatalog>();
        _coverProvider = new Mock<ICoverProvider>();
        _coverArtService = new Mock<ICoverArtService>();
        _viewFactory = new Mock<IViewFactory>();
        _host = new Mock<IModalWindowHost>();
        _view = new Mock<IMyWaveView>();
        _ui = new FakeUiDispatcher();
        _errorHandler = new Mock<IErrorHandler>();
        _eventBus = new EventBus();

        _viewFactory.Setup(f => f.Create<IMyWaveView>()).Returns(_view.Object);
        _view.SetupGet(v => v.CoverSize).Returns((CoverWidth, CoverHeight));

        _presenter = new MyWaveWindowPresenter(
            _queue.Object, _trackCatalog.Object, _coverProvider.Object, _coverArtService.Object,
            _eventBus, _viewFactory.Object, _host.Object, _ui, _errorHandler.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };

    private static Playlist Wave(string? description = null)
        => new("my-wave", "Моя волна") { Description = description! };

    private void SetupTrackWithCover(string trackId, CoverArt? art)
    {
        _trackCatalog.Setup(p => p.GetAsync(K(trackId))).ReturnsAsync(Track(trackId));
        _coverProvider.Setup(c => c.DownloadCoverAsync(It.Is<Track>(t => t.Id == trackId))).ReturnsAsync($"covers/{trackId}.png");
        _coverArtService
            .Setup(c => c.RenderAsync($"covers/{trackId}.png", CoverWidth, CoverHeight, It.IsAny<CancellationToken>()))
            .ReturnsAsync(art);
    }

    // ── Описание волны ────────────────────────────────────────────────────

    [Test]
    public void ShowWindow_UsesPlaylistDescriptionAsWaveDescription()
    {
        _presenter.ShowWindow(Wave("Волна дня"));

        _view.Verify(v => v.SetWaveDescription("Волна дня"), Times.Once);
    }

    [Test]
    public void ShowWindow_WithoutDescription_UsesDefaultText()
    {
        _presenter.ShowWindow(Wave());

        _view.Verify(v => v.SetWaveDescription("Персональная радиостанция"), Times.Once);
    }

    // ── Текущий трек и обложка ────────────────────────────────────────────

    [Test]
    public void ShowWindow_WithCurrentTrack_LoadsTrackAndCover()
    {
        var art = new CoverArt(1, 1, []);
        _queue.SetupGet(q => q.CurrentTrack).Returns(K("t1"));
        SetupTrackWithCover("t1", art);

        _presenter.ShowWindow(Wave());

        _view.Verify(v => v.SetTrack(It.Is<Track>(t => t.Id == "t1")), Times.Once);
        _coverArtService.Verify(
            c => c.RenderAsync("covers/t1.png", CoverWidth, CoverHeight, It.IsAny<CancellationToken>()),
            Times.Once);
        _view.Verify(v => v.SetCover(art), Times.Once);
    }

    [Test]
    public void ShowWindow_WithoutCurrentTrack_DoesNotLoad()
    {
        _queue.SetupGet(q => q.CurrentTrack).Returns((TrackKey?)null);

        _presenter.ShowWindow(Wave());

        _trackCatalog.Verify(p => p.GetAsync(It.IsAny<TrackKey>()), Times.Never);
        _host.Verify(h => h.Show(_view.Object), Times.Once);
    }

    [Test]
    public void ShowWindow_WhenTrackLoadFails_ReportsViaErrorHandler()
    {
        var ex = new InvalidOperationException("boom");
        _queue.SetupGet(q => q.CurrentTrack).Returns(K("t1"));
        _trackCatalog.Setup(p => p.GetAsync(K("t1"))).ThrowsAsync(ex);

        _presenter.ShowWindow(Wave());

        _errorHandler.Verify(e => e.Handle(ex), Times.Once);
    }

    // ── Следующий трек ────────────────────────────────────────────────────

    [Test]
    public void ShowWindow_WithNextTrack_ShowsNextLabel()
    {
        _queue.SetupGet(q => q.CurrentTrack).Returns(K("t1"));
        _queue.SetupGet(q => q.PeekNext).Returns(K("t2"));
        _trackCatalog.Setup(p => p.GetAsync(It.IsAny<TrackKey>()))
            .ReturnsAsync((TrackKey key) => Track(key.TrackId));

        _presenter.ShowWindow(Wave());

        _view.Verify(v => v.SetNextTrackLabel("A-t2 — T-t2"), Times.Once);
    }

    [Test]
    public void ShowWindow_WithoutNextTrack_ClearsLabel()
    {
        _queue.SetupGet(q => q.CurrentTrack).Returns(K("t1"));
        _queue.SetupGet(q => q.PeekNext).Returns((TrackKey?)null);
        _trackCatalog.Setup(p => p.GetAsync(K("t1"))).ReturnsAsync(Track("t1"));

        _presenter.ShowWindow(Wave());

        _view.Verify(v => v.SetNextTrackLabel(null), Times.Once);
    }

    // ── Размер окна ───────────────────────────────────────────────────────

    /// <summary>
    /// После изменения размера окна обложка перерисовывается в новом размере:
    /// старый ASCII-буфер не растягивается и не обрезается.
    /// </summary>
    [Test]
    public void ShowWindow_WhenCoverAreaChanges_RendersCoverAtNewSize()
    {
        var size = (Width: 100, Height: 55);
        _view.SetupGet(v => v.CoverSize).Returns(() => size);

        var art = new CoverArt(1, 1, []);
        var track = Track("t1");
        _queue.SetupGet(q => q.CurrentTrack).Returns(K("t1"));
        _trackCatalog.Setup(p => p.GetAsync(K("t1"))).ReturnsAsync(track);
        _coverProvider.Setup(c => c.DownloadCoverAsync(track)).ReturnsAsync("covers/t1.png");
        _coverArtService
            .Setup(c => c.RenderAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(art);

        _host.Setup(h => h.Show(_view.Object)).Callback(() =>
        {
            size = (Width: 80, Height: 44);
            _view.Raise(v => v.CoverSizeChanged += null);

            // В тесте окно закрывается сразу, а в жизни — по Esc. Даём отложенной
            // перерисовке завершиться, пока презентер ещё подписан на событие.
            Thread.Sleep(400);
        });

        _presenter.ShowWindow(Wave());

        _coverArtService.Verify(
            c => c.RenderAsync("covers/t1.png", 80, 44, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── События шины ──────────────────────────────────────────────────────

    [Test]
    public void ShowWindow_OnTrackChangedWhileOpen_LoadsNewTrack()
    {
        _queue.SetupGet(q => q.CurrentTrack).Returns((TrackKey?)null);
        _trackCatalog.Setup(p => p.GetAsync(It.IsAny<TrackKey>()))
            .ReturnsAsync((TrackKey key) => Track(key.TrackId));
        _host.Setup(h => h.Show(_view.Object))
            .Callback(() => _eventBus.Publish(new TrackChangedEvent(K("t5"))));

        _presenter.ShowWindow(Wave());

        _view.Verify(v => v.SetTrack(It.Is<Track>(t => t.Id == "t5")), Times.Once);
    }

    [Test]
    public void ShowWindow_AfterClose_UnsubscribesFromTrackChanged()
    {
        _queue.SetupGet(q => q.CurrentTrack).Returns((TrackKey?)null);

        _presenter.ShowWindow(Wave());

        _eventBus.Publish(new TrackChangedEvent(K("t9")));

        _trackCatalog.Verify(p => p.GetAsync(K("t9")), Times.Never);
    }
}
