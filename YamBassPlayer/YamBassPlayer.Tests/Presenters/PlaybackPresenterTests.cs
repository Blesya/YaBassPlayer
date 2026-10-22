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
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private Mock<ITrackFileProvider> _trackFileProvider = null!;
    private Mock<IPlaybackQueue> _playbackQueue = null!;
    private Mock<ITrackInfoProvider> _trackInfoProvider = null!;
    private Mock<IListenTimer> _listenTimer = null!;
    private Mock<IAudioPlayer> _audioPlayer = null!;
    private Mock<IPlayStatusPresenter> _playStatusPresenter = null!;
    private Mock<IMyWavePresenter> _myWavePresenter = null!;
    private Mock<ITrackAboutPresenter> _trackAboutPresenter = null!;
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
        _trackAboutPresenter = new Mock<ITrackAboutPresenter>();

        _presenter = new PlaybackPresenter(
            _trackFileProvider.Object,
            _playbackQueue.Object,
            _trackInfoProvider.Object,
            _listenTimer.Object,
            _audioPlayer.Object,
            _playStatusPresenter.Object,
            _myWavePresenter.Object,
            _trackAboutPresenter.Object);
    }

    private void SetupTrack(string id)
        => _trackInfoProvider.Setup(p => p.GetTrackInfoById(K(id)))
            .ReturnsAsync(new Track($"Title-{id}", $"Artist-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value });

    private static Track Matches(string id) => It.Is<Track>(t => t.Key == K(id));

    // ── Загрузка выбранного трека ─────────────────────────────────────────

    [Test]
    public async Task PlaySelectedTrackAsync_DownloadsPlaysAndReportsStatus()
    {
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync("/tmp/t1.mp3");

        await _presenter.PlaySelectedTrackAsync(K("t1"));

        Assert.Multiple(() =>
        {
            _audioPlayer.Verify(a => a.Play("/tmp/t1.mp3", It.IsAny<string>()), Times.Once);
            _listenTimer.Verify(l => l.OnTrackStart(K("t1"), PlaylistContext.Regular), Times.Once);
            _playStatusPresenter.Verify(s => s.SetCurrentTrack(K("t1")), Times.Once);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_WhenDownloadFails_DoesNotPlay()
    {
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync(string.Empty);

        await _presenter.PlaySelectedTrackAsync(K("t1"));

        Assert.Multiple(() =>
        {
            _audioPlayer.Verify(a => a.Play(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _listenTimer.Verify(l => l.OnTrackStart(It.IsAny<TrackKey>(), It.IsAny<PlaylistContext>()), Times.Never);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_WhenTrackUnavailable_OpensTrackAbout()
    {
        SetupTrack("t1");
        _trackFileProvider
            .Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>()))
            .ThrowsAsync(new TrackUnavailableException("t1"));

        await _presenter.PlaySelectedTrackAsync(K("t1"));

        Assert.Multiple(() =>
        {
            _audioPlayer.Verify(a => a.Play(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _trackAboutPresenter.Verify(
                p => p.ShowTrackAboutAsync(K("t1"), true),
                Times.Once);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_AutomaticUnavailable_SkipsWithoutWindow()
    {
        SetupTrack("t1");
        _trackFileProvider
            .Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>()))
            .ThrowsAsync(new TrackUnavailableException("t1"));
        _playbackQueue.SetupGet(q => q.TrackKeys).Returns(new[] { K("t1"), K("t2") });

        await _presenter.PlaySelectedTrackAsync(K("t1"), isAutomatic: true);

        Assert.Multiple(() =>
        {
            _trackAboutPresenter.Verify(
                p => p.ShowTrackAboutAsync(It.IsAny<TrackKey>(), It.IsAny<bool>()),
                Times.Never);
            _playbackQueue.Verify(q => q.NextAutomatic(), Times.Once);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_ReportsDownloadProgressInTitle()
    {
        SetupTrack("t1");
        IProgress<DownloadProgress>? capturedProgress = null;
        _trackFileProvider
            .Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>()))
            .Callback<Track, IProgress<DownloadProgress>>((_, progress) => capturedProgress = progress)
            .ReturnsAsync("/tmp/t1.mp3");

        var titles = new List<string>();
        _playStatusPresenter.Setup(s => s.SetTitle(It.IsAny<string>()))
            .Callback<string>(title => titles.Add(title));

        await _presenter.PlaySelectedTrackAsync(K("t1"));

        Assert.That(capturedProgress, Is.Not.Null);
        capturedProgress!.Report(new DownloadProgress(5_000_000, 10_000_000));

        Assert.That(titles, Has.Some.EqualTo("Загружается трек: Artist-t1 - Title-t1 (50%)"));
        _playStatusPresenter.Verify(s => s.SetPlayStatus("Загрузка: 50%"), Times.Once);
    }

    [Test]
    public async Task PlaySelectedTrackAsync_WhenTotalSizeUnknown_ShowsBytes()
    {
        SetupTrack("t1");
        IProgress<DownloadProgress>? capturedProgress = null;
        _trackFileProvider
            .Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>()))
            .Callback<Track, IProgress<DownloadProgress>>((_, progress) => capturedProgress = progress)
            .ReturnsAsync("/tmp/t1.mp3");

        var titles = new List<string>();
        _playStatusPresenter.Setup(s => s.SetTitle(It.IsAny<string>()))
            .Callback<string>(title => titles.Add(title));

        await _presenter.PlaySelectedTrackAsync(K("t1"));

        capturedProgress!.Report(new DownloadProgress(1_048_576, 0));

        Assert.That(titles, Has.Some.EqualTo("Загружается трек: Artist-t1 - Title-t1 (1 МБ)"));
    }

    // ── Ветка «Моей волны» ────────────────────────────────────────────────

    [Test]
    public async Task PlaySelectedTrackAsync_MyWave_NotifiesStartedAndFetchesMoreWhenNoNext()
    {
        _presenter.SetPlaylistContext(PlaylistContext.Radio);
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync("/tmp/t1.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(false);

        await _presenter.PlaySelectedTrackAsync(K("t1"));

        Assert.Multiple(() =>
        {
            _listenTimer.Verify(l => l.OnTrackStart(K("t1"), PlaylistContext.Radio), Times.Once);
            _myWavePresenter.Verify(m => m.NotifyTrackStartedAsync(K("t1")), Times.Once);
            _myWavePresenter.Verify(m => m.FetchMoreTracksAsync(), Times.Once);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_MyWave_WithNext_DoesNotFetchMore()
    {
        _presenter.SetPlaylistContext(PlaylistContext.Radio);
        SetupTrack("t1");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync("/tmp/t1.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);

        await _presenter.PlaySelectedTrackAsync(K("t1"));

        _myWavePresenter.Verify(m => m.FetchMoreTracksAsync(), Times.Never);
    }

    [Test]
    public async Task PlaySelectedTrackAsync_MyWave_NotifiesFinishedForPreviousTrack()
    {
        _presenter.SetPlaylistContext(PlaylistContext.Radio);
        SetupTrack("t1");
        SetupTrack("t2");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(It.IsAny<Track>(), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync("/tmp/x.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);
        _audioPlayer.Setup(a => a.GetCurrentPosition()).Returns(TimeSpan.FromSeconds(10));

        await _presenter.PlaySelectedTrackAsync(K("t1"));
        await _presenter.PlaySelectedTrackAsync(K("t2"));

        _myWavePresenter.Verify(m => m.NotifyTrackFinishedAsync(K("t1"), 10), Times.Once);
    }

    [Test]
    public async Task MarkMyWaveSkipPending_MakesNextPlayNotifySkipped()
    {
        _presenter.SetPlaylistContext(PlaylistContext.Radio);
        SetupTrack("t1");
        SetupTrack("t2");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(It.IsAny<Track>(), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync("/tmp/x.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);
        _audioPlayer.Setup(a => a.GetCurrentPosition()).Returns(TimeSpan.FromSeconds(5));

        await _presenter.PlaySelectedTrackAsync(K("t1"));
        _presenter.MarkMyWaveSkipPending();
        await _presenter.PlaySelectedTrackAsync(K("t2"));

        Assert.Multiple(() =>
        {
            _myWavePresenter.Verify(m => m.NotifyTrackSkippedAsync(K("t1"), 5), Times.Once);
            _myWavePresenter.Verify(m => m.NotifyTrackFinishedAsync(It.IsAny<TrackKey>(), It.IsAny<double>()), Times.Never);
        });
    }

    [Test]
    public async Task SetPlaylistContext_Regular_StopsNotifyingMyWave()
    {
        _presenter.SetPlaylistContext(PlaylistContext.Radio);
        SetupTrack("t1");
        SetupTrack("t2");
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(It.IsAny<Track>(), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync("/tmp/x.mp3");
        _playbackQueue.SetupGet(q => q.HasNext).Returns(true);
        _audioPlayer.Setup(a => a.GetCurrentPosition()).Returns(TimeSpan.FromSeconds(7));

        await _presenter.PlaySelectedTrackAsync(K("t1"));
        _presenter.SetPlaylistContext(PlaylistContext.Regular);
        await _presenter.PlaySelectedTrackAsync(K("t2"));

        Assert.Multiple(() =>
        {
            _myWavePresenter.Verify(m => m.NotifyTrackFinishedAsync(It.IsAny<TrackKey>(), It.IsAny<double>()), Times.Never);
            _listenTimer.Verify(l => l.OnTrackStart(K("t2"), PlaylistContext.Regular), Times.Once);
        });
    }

    // ── Перекрытие запросов (быстрые клики) ───────────────────────────────

    [Test]
    public async Task PlaySelectedTrackAsync_WhenSupersededByNewerRequest_DoesNotPlayStaleTrack()
    {
        SetupTrack("t1");
        SetupTrack("t2");

        var staleDownloadStarted = new TaskCompletionSource();
        var staleDownloadRelease = new TaskCompletionSource<string>();
        _trackFileProvider
            .Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>()))
            .Returns(async (Track _, IProgress<DownloadProgress> __) =>
            {
                staleDownloadStarted.SetResult();
                return await staleDownloadRelease.Task;
            });
        _trackFileProvider
            .Setup(p => p.DownloadTrackAsync(Matches("t2"), It.IsAny<IProgress<DownloadProgress>>()))
            .ReturnsAsync("/tmp/t2.mp3");

        Task stalePlay = _presenter.PlaySelectedTrackAsync(K("t1"));
        await staleDownloadStarted.Task;

        await _presenter.PlaySelectedTrackAsync(K("t2"));

        staleDownloadRelease.SetResult("/tmp/t1.mp3");
        await stalePlay;

        Assert.Multiple(() =>
        {
            _audioPlayer.Verify(a => a.Play("/tmp/t1.mp3", It.IsAny<string>()), Times.Never);
            _audioPlayer.Verify(a => a.Play("/tmp/t2.mp3", It.IsAny<string>()), Times.Once);
            _listenTimer.Verify(l => l.OnTrackStart(K("t1"), It.IsAny<PlaylistContext>()), Times.Never);
        });
    }

    [Test]
    public async Task PlaySelectedTrackAsync_SameTrackClickedTwice_PlaysOnceForLatestRequest()
    {
        SetupTrack("t1");

        var downloadStarted = new TaskCompletionSource();
        var downloadRelease = new TaskCompletionSource<string>();
        _trackFileProvider
            .Setup(p => p.DownloadTrackAsync(Matches("t1"), It.IsAny<IProgress<DownloadProgress>>()))
            .Returns(async (Track _, IProgress<DownloadProgress> __) =>
            {
                downloadStarted.TrySetResult();
                return await downloadRelease.Task;
            });

        Task first = _presenter.PlaySelectedTrackAsync(K("t1"));
        await downloadStarted.Task;
        Task second = _presenter.PlaySelectedTrackAsync(K("t1"));

        downloadRelease.SetResult("/tmp/t1.mp3");
        await Task.WhenAll(first, second);

        _audioPlayer.Verify(a => a.Play("/tmp/t1.mp3", It.IsAny<string>()), Times.Once);
        _listenTimer.Verify(l => l.OnTrackStart(K("t1"), PlaylistContext.Regular), Times.Once);
    }

    // ── Предзагрузка следующего трека ─────────────────────────────────────

    [Test]
    public async Task PreloadNextTrackAsync_SkipsWhenTrackAlreadyDownloaded()
    {
        _playbackQueue.SetupGet(q => q.PeekNext).Returns(K("n1"));
        SetupTrack("n1");
        _trackFileProvider.Setup(p => p.IsTrackDownloaded(It.Is<Track>(t => t.Key == K("n1")))).Returns(true);

        await _presenter.PreloadNextTrackAsync();

        _trackFileProvider.Verify(p => p.DownloadTrackAsync(It.IsAny<Track>(), It.IsAny<IProgress<DownloadProgress>>()), Times.Never);
    }

    [Test]
    public async Task PreloadNextTrackAsync_DownloadsWhenNotCached()
    {
        _playbackQueue.SetupGet(q => q.PeekNext).Returns(K("n1"));
        SetupTrack("n1");
        _trackFileProvider.Setup(p => p.IsTrackDownloaded(It.IsAny<Track>())).Returns(false);
        _trackFileProvider.Setup(p => p.DownloadTrackAsync(Matches("n1"), It.IsAny<IProgress<DownloadProgress>>())).ReturnsAsync("/tmp/n1.mp3");

        await _presenter.PreloadNextTrackAsync();

        _trackFileProvider.Verify(p => p.DownloadTrackAsync(Matches("n1"), It.IsAny<IProgress<DownloadProgress>>()), Times.Once);
    }

    [Test]
    public async Task PreloadNextTrackAsync_WhenNoNext_DoesNothing()
    {
        _playbackQueue.SetupGet(q => q.PeekNext).Returns((TrackKey?)null);

        await _presenter.PreloadNextTrackAsync();

        _trackFileProvider.Verify(p => p.DownloadTrackAsync(It.IsAny<Track>(), It.IsAny<IProgress<DownloadProgress>>()), Times.Never);
    }
}
