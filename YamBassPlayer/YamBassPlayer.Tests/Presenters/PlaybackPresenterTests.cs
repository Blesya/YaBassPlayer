using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class PlaybackPresenterTests
{
    private Mock<ITrackFileProvider> _trackFileProvider = null!;
    private Mock<IPlaybackQueue> _playbackQueue = null!;
    private Mock<ITrackInfoProvider> _trackInfoProvider = null!;
    private Mock<IListenTimer> _listenTimer = null!;
    private Mock<IAudioPlayer> _audioPlayer = null!;
    private Mock<IPlayStatusPresenter> _playStatusPresenter = null!;
    private Mock<IMyWavePresenter> _myWavePresenter = null!;
    private PlaybackPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _trackFileProvider = new Mock<ITrackFileProvider>();
        _playbackQueue = new Mock<IPlaybackQueue>();
        _trackInfoProvider = new Mock<ITrackInfoProvider>();
        _listenTimer = new Mock<IListenTimer>();
        _audioPlayer = new Mock<IAudioPlayer>();
        _playStatusPresenter = new Mock<IPlayStatusPresenter>();
        _myWavePresenter = new Mock<IMyWavePresenter>();

        _presenter = new PlaybackPresenter(
            _trackFileProvider.Object,
            _playbackQueue.Object,
            _trackInfoProvider.Object,
            _listenTimer.Object,
            _audioPlayer.Object,
            _playStatusPresenter.Object,
            _myWavePresenter.Object);
    }

    private void SetupTrack(string id)
        => _trackInfoProvider.Setup(p => p.GetTrackInfoById(id))
            .ReturnsAsync(new Track($"Title-{id}", $"Artist-{id}", "Album", id));

    // ── Загрузка выбранного трека ─────────────────────────────────────────

    [Test]
    public async Task PlaySelectedTrackAsync_DownloadsPlaysAndReportsStatus()
    {
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync("t1")).ReturnsAsync("/tmp/t1.mp3");

        await _presenter.PlaySelectedTrackAsync("t1");

        Assert.Multiple(() =>
        {
            _audioPlayer.Verify(a => a.Play("/tmp/t1.mp3", It.IsAny<string>()), Times.Once);
            _listenTimer.Verify(l => l.OnTrackStart("t1", ListenSource.Regular), Times.Once);
            _playStatusPresenter.Verify(s => s.SetCurrentTrack("t1", SourceIds.Yandex), Times.Once);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_WhenDownloadFails_DoesNotPlay()
    {
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync("t1")).ReturnsAsync(string.Empty);

        await _presenter.PlaySelectedTrackAsync("t1");

        Assert.Multiple(() =>
        {
            _audioPlayer.Verify(a => a.Play(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _listenTimer.Verify(l => l.OnTrackStart(It.IsAny<string>(), It.IsAny<ListenSource>()), Times.Never);
        });
    }

    // ── Ветка «Моей волны» ────────────────────────────────────────────────

    [Test]
    public async Task PlaySelectedTrackAsync_MyWave_NotifiesStartedAndFetchesMoreWhenNoNext()
    {
        _presenter.SetPlaylistType(PlaylistType.MyWave);
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync("t1")).ReturnsAsync("/tmp/t1.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(false);

        await _presenter.PlaySelectedTrackAsync("t1");

        Assert.Multiple(() =>
        {
            _listenTimer.Verify(l => l.OnTrackStart("t1", ListenSource.MyWave), Times.Once);
            _myWavePresenter.Verify(m => m.NotifyTrackStartedAsync("t1"), Times.Once);
            _myWavePresenter.Verify(m => m.FetchMoreTracksAsync(), Times.Once);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_MyWave_WithNext_DoesNotFetchMore()
    {
        _presenter.SetPlaylistType(PlaylistType.MyWave);
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync("t1")).ReturnsAsync("/tmp/t1.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);

        await _presenter.PlaySelectedTrackAsync("t1");

        _myWavePresenter.Verify(m => m.FetchMoreTracksAsync(), Times.Never);
    }

    [Test]
    public async Task PlaySelectedTrackAsync_MyWave_NotifiesFinishedForPreviousTrack()
    {
        _presenter.SetPlaylistType(PlaylistType.MyWave);
        SetupTrack("t1");
        SetupTrack("t2");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(It.IsAny<string>())).ReturnsAsync("/tmp/x.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);
        _audioPlayer.Setup(a => a.GetCurrentPosition()).Returns(TimeSpan.FromSeconds(10));

        await _presenter.PlaySelectedTrackAsync("t1");
        await _presenter.PlaySelectedTrackAsync("t2");

        _myWavePresenter.Verify(m => m.NotifyTrackFinishedAsync("t1", 10), Times.Once);
    }

    [Test]
    public async Task MarkMyWaveSkipPending_MakesNextPlayNotifySkipped()
    {
        _presenter.SetPlaylistType(PlaylistType.MyWave);
        SetupTrack("t1");
        SetupTrack("t2");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(It.IsAny<string>())).ReturnsAsync("/tmp/x.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);
        _audioPlayer.Setup(a => a.GetCurrentPosition()).Returns(TimeSpan.FromSeconds(5));

        await _presenter.PlaySelectedTrackAsync("t1");
        _presenter.MarkMyWaveSkipPending();
        await _presenter.PlaySelectedTrackAsync("t2");

        Assert.Multiple(() =>
        {
            _myWavePresenter.Verify(m => m.NotifyTrackSkippedAsync("t1", 5), Times.Once);
            _myWavePresenter.Verify(m => m.NotifyTrackFinishedAsync(It.IsAny<string>(), It.IsAny<double>()), Times.Never);
        });
    }

    [Test]
    public async Task SetPlaylistType_NonMyWave_StopsNotifyingMyWave()
    {
        _presenter.SetPlaylistType(PlaylistType.MyWave);
        SetupTrack("t1");
        SetupTrack("t2");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(It.IsAny<string>())).ReturnsAsync("/tmp/x.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);
        _audioPlayer.Setup(a => a.GetCurrentPosition()).Returns(TimeSpan.FromSeconds(7));

        await _presenter.PlaySelectedTrackAsync("t1");
        _presenter.SetPlaylistType(PlaylistType.Favorite);
        await _presenter.PlaySelectedTrackAsync("t2");

        Assert.Multiple(() =>
        {
            _myWavePresenter.Verify(m => m.NotifyTrackFinishedAsync(It.IsAny<string>(), It.IsAny<double>()), Times.Never);
            _listenTimer.Verify(l => l.OnTrackStart("t2", ListenSource.Regular), Times.Once);
        });
    }

    // ── Предзагрузка следующего трека ─────────────────────────────────────

    [Test]
    public async Task PreloadNextTrackAsync_SkipsWhenTrackAlreadyDownloaded()
    {
        _playbackQueue.SetupGet(q => q.PeekNextTrackId).Returns("n1");
        _trackFileProvider.Setup(p => p.IsTrackDownloaded("n1")).Returns(true);

        await _presenter.PreloadNextTrackAsync();

        _trackFileProvider.Verify(p => p.DownloadTrackAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PreloadNextTrackAsync_DownloadsWhenNotCached()
    {
        _playbackQueue.SetupGet(q => q.PeekNextTrackId).Returns("n1");
        _trackFileProvider.Setup(p => p.IsTrackDownloaded("n1")).Returns(false);
        SetupTrack("n1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync("n1")).ReturnsAsync("/tmp/n1.mp3");

        await _presenter.PreloadNextTrackAsync();

        _trackFileProvider.Verify(p => p.DownloadTrackAsync("n1"), Times.Once);
    }

    [Test]
    public async Task PreloadNextTrackAsync_WhenNoNext_DoesNothing()
    {
        _playbackQueue.SetupGet(q => q.PeekNextTrackId).Returns((string?)null);

        await _presenter.PreloadNextTrackAsync();

        _trackFileProvider.Verify(p => p.DownloadTrackAsync(It.IsAny<string>()), Times.Never);
    }
}
