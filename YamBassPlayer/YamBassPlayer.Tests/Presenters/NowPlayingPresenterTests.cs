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
public sealed class NowPlayingPresenterTests
{
    private Mock<IAudioPlayer> _audio = null!;
    private Mock<IPlaybackQueue> _queue = null!;
    private Mock<ITrackCatalog> _trackCatalog = null!;
    private Mock<IViewFactory> _viewFactory = null!;
    private Mock<IModalWindowHost> _host = null!;
    private Mock<INowPlayingView> _view = null!;
    private FakeUiDispatcher _ui = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private EventBus _eventBus = null!;
    private NowPlayingPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _audio = new Mock<IAudioPlayer>();
        _queue = new Mock<IPlaybackQueue>();
        _trackCatalog = new Mock<ITrackCatalog>();
        _viewFactory = new Mock<IViewFactory>();
        _host = new Mock<IModalWindowHost>();
        _view = new Mock<INowPlayingView>();
        _ui = new FakeUiDispatcher();
        _errorHandler = new Mock<IErrorHandler>();
        _eventBus = new EventBus();

        _viewFactory.Setup(f => f.Create<INowPlayingView>()).Returns(_view.Object);

        _presenter = new NowPlayingPresenter(
            _audio.Object, _queue.Object, _trackCatalog.Object, _eventBus,
            _viewFactory.Object, _host.Object, _ui, _errorHandler.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);

    // ── Загрузка трека ────────────────────────────────────────────────────

    [Test]
    public void ShowNowPlaying_WithCurrentTrack_LoadsAndDisplaysIt()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns("t1");
        _trackCatalog.Setup(p => p.GetAsync("t1")).ReturnsAsync(Track("t1"));

        _presenter.ShowNowPlaying();

        _view.Verify(v => v.SetTrack(It.Is<Track>(t => t.Id == "t1")), Times.Once);
        _host.Verify(h => h.Show(_view.Object), Times.Once);
    }

    [Test]
    public void ShowNowPlaying_WithoutCurrentTrack_DoesNotLoad()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);

        _presenter.ShowNowPlaying();

        _trackCatalog.Verify(p => p.GetAsync(It.IsAny<string>()), Times.Never);
        _host.Verify(h => h.Show(_view.Object), Times.Once);
    }

    [Test]
    public void ShowNowPlaying_WhenProviderFails_ReportsViaErrorHandler()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns("t1");
        var ex = new InvalidOperationException("boom");
        _trackCatalog.Setup(p => p.GetAsync("t1")).ThrowsAsync(ex);

        _presenter.ShowNowPlaying();

        _errorHandler.Verify(e => e.Handle(ex), Times.Once);
    }

    // ── События шины ──────────────────────────────────────────────────────

    [Test]
    public void ShowNowPlaying_OnTrackChangedWhileOpen_LoadsNewTrack()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns("t1");
        _trackCatalog.Setup(p => p.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => Track(id));
        _host.Setup(h => h.Show(_view.Object))
            .Callback(() => _eventBus.Publish(new TrackChangedEvent("t2")));

        _presenter.ShowNowPlaying();

        _view.Verify(v => v.SetTrack(It.Is<Track>(t => t.Id == "t2")), Times.Once);
    }

    [Test]
    public void ShowNowPlaying_AfterClose_UnsubscribesFromTrackChanged()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);

        _presenter.ShowNowPlaying();

        _eventBus.Publish(new TrackChangedEvent("t9"));

        _trackCatalog.Verify(p => p.GetAsync("t9"), Times.Never);
    }

    // ── Таймер спектра ────────────────────────────────────────────────────

    [Test]
    public void ShowNowPlaying_SpectrumTimer_UsesWaveformDataInWaveformMode()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);
        _view.SetupGet(v => v.SpectrumDataType).Returns(SpectrumDataType.Waveform);
        _audio.SetupGet(a => a.IsPlayed).Returns(true);
        var data = new float[] { 1f, 2f };
        _audio.Setup(a => a.GetWaveformData(512)).Returns(data);
        _host.Setup(h => h.Show(_view.Object)).Callback(() => _ui.Tick());

        _presenter.ShowNowPlaying();

        _audio.Verify(a => a.GetWaveformData(512), Times.Once);
        _view.Verify(v => v.SetSpectrumData(data), Times.Once);
    }

    [Test]
    public void ShowNowPlaying_SpectrumTimer_UsesChannelDataInFftMode()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);
        _view.SetupGet(v => v.SpectrumDataType).Returns(SpectrumDataType.Fft);
        _audio.SetupGet(a => a.IsPlayed).Returns(true);
        var data = new float[] { 3f };
        _audio.Setup(a => a.ChannelGetData()).Returns(data);
        _host.Setup(h => h.Show(_view.Object)).Callback(() => _ui.Tick());

        _presenter.ShowNowPlaying();

        _audio.Verify(a => a.ChannelGetData(), Times.Once);
        _view.Verify(v => v.SetSpectrumData(data), Times.Once);
    }

    [Test]
    public void ShowNowPlaying_SpectrumTimer_WhenNotPlaying_DoesNotFetchData()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);
        _audio.SetupGet(a => a.IsPlayed).Returns(false);
        _host.Setup(h => h.Show(_view.Object)).Callback(() => _ui.Tick());

        _presenter.ShowNowPlaying();

        _audio.Verify(a => a.ChannelGetData(), Times.Never);
        _view.Verify(v => v.SetSpectrumData(It.IsAny<float[]>()), Times.Never);
    }

    [Test]
    public void ShowNowPlaying_AfterClose_SpectrumTimerStops()
    {
        _queue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);
        _audio.SetupGet(a => a.IsPlayed).Returns(true);

        _presenter.ShowNowPlaying();

        Assert.That(_ui.Tick(), Is.False, "После закрытия таймер должен вернуть false и остановиться.");
    }
}
