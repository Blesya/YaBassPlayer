using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class MyWavePresenterTests
{
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private Mock<IMusicSource> _source = null!;
    private Mock<IRadioSource> _radio = null!;
    private Mock<IMusicSourceRegistry> _registry = null!;
    private Mock<ITrackInfoProvider> _trackInfoProvider = null!;
    private Mock<ITrackRepositoryCache> _cache = null!;
    private Mock<ITracksPresenter> _tracksPresenter = null!;
    private Mock<IPlaybackQueue> _queue = null!;
    private Mock<IPlayStatusPresenter> _playStatus = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private MyWavePresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _source = new Mock<IMusicSource>();
        _radio = _source.As<IRadioSource>();
        _source.Setup(s => s.Id).Returns(YandexMusicSourceId.Value);
        _registry = new Mock<IMusicSourceRegistry>();
        _registry.Setup(r => r.Get(YandexMusicSourceId.Value)).Returns(_source.Object);
        _registry.Setup(r => r.Sources).Returns(new[] { _source.Object });

        _trackInfoProvider = new Mock<ITrackInfoProvider>();
        _cache = new Mock<ITrackRepositoryCache>();
        _tracksPresenter = new Mock<ITracksPresenter>();
        _queue = new Mock<IPlaybackQueue>();
        _playStatus = new Mock<IPlayStatusPresenter>();
        _errorHandler = new Mock<IErrorHandler>();

        _tracksPresenter.Setup(t => t.LoadTracksFor(It.IsAny<Playlist>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _presenter = new MyWavePresenter(
            _registry.Object, _trackInfoProvider.Object, _cache.Object,
            _tracksPresenter.Object, _queue.Object, _playStatus.Object, _errorHandler.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };

    private void SetupBatch(params string[] ids)
    {
        var batch = new RadioBatch(ids.Select(K).ToList(), ids.Select(Track).ToList());
        _radio.Setup(r => r.FetchNextBatchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(batch);
    }

    // ── Запуск волны ──────────────────────────────────────────────────────

    [Test]
    public async Task StartMyWaveAsync_WhenStationFails_ReturnsNullAndReports()
    {
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _presenter.StartMyWaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            _playStatus.Verify(p => p.SetPlayStatus(It.Is<string>(s => s.Contains("Не удалось"))), Times.Once);
        });
    }

    [Test]
    public async Task StartMyWaveAsync_WhenBatchEmpty_ReturnsNullAndReports()
    {
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch();

        var result = await _presenter.StartMyWaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            _playStatus.Verify(p => p.SetPlayStatus(It.Is<string>(s => s.Contains("Нет треков"))), Times.Once);
        });
    }

    [Test]
    public async Task StartMyWaveAsync_HappyPath_SavesReplacesCacheLoadsAndStartsQueue()
    {
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch("a", "b");

        var result = await _presenter.StartMyWaveAsync();

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            _trackInfoProvider.Verify(p => p.SaveAsync(It.IsAny<Track>()), Times.Exactly(2));
            _cache.Verify(c => c.ReplaceMyWaveTracks(It.IsAny<IEnumerable<Track>>()), Times.Once);
            _tracksPresenter.Verify(t => t.LoadTracksFor(result!, It.IsAny<CancellationToken>()), Times.Once);
            _queue.Verify(q => q.SetQueue(It.Is<IEnumerable<TrackKey>>(x => x.SequenceEqual(new[] { K("a"), K("b") })), 0), Times.Once);
        });
        Assert.That(result!.Kind, Is.EqualTo("my-wave"));
        Assert.Multiple(() =>
        {
            Assert.That(result.Category, Is.EqualTo(PlaylistCategory.Transient));
            Assert.That(result.Context, Is.EqualTo(PlaylistContext.Radio));
            Assert.That(result.SourceId, Is.EqualTo(YandexMusicSourceId.Value));
        });
    }

    [Test]
    public async Task StartMyWaveFromTrackAsync_UsesTrackWaveAndSeedDescription()
    {
        _trackInfoProvider.Setup(p => p.GetTrackInfoById(K("seed"))).ReturnsAsync(Track("seed"));
        _radio.Setup(r => r.StartTrackRadioAsync(K("seed"), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch("x");

        var result = await _presenter.StartMyWaveFromTrackAsync(K("seed"));

        Assert.Multiple(() =>
        {
            _radio.Verify(r => r.StartTrackRadioAsync(K("seed"), It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Description, Does.Contain("A-seed"));
        });
    }

    // ── Догрузка батча ────────────────────────────────────────────────────

    [Test]
    public async Task FetchMoreTracksAsync_AppendsToCacheAndQueue()
    {
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch("a");
        await _presenter.StartMyWaveAsync();

        SetupBatch("c", "d");
        await _presenter.FetchMoreTracksAsync();

        Assert.Multiple(() =>
        {
            _cache.Verify(c => c.AppendMyWaveTracks(It.IsAny<IEnumerable<Track>>()), Times.Once);
            _queue.Verify(q => q.AddToQueue(It.Is<IEnumerable<TrackKey>>(x => x.SequenceEqual(new[] { K("c"), K("d") }))), Times.Once);
        });
    }

    [Test]
    public async Task FetchMoreTracksAsync_WhenBatchEmpty_DoesNothing()
    {
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch("a");
        await _presenter.StartMyWaveAsync();

        SetupBatch();
        await _presenter.FetchMoreTracksAsync();

        _cache.Verify(c => c.AppendMyWaveTracks(It.IsAny<IEnumerable<Track>>()), Times.Never);
    }

    // ── Обработка ошибок ──────────────────────────────────────────────────

    [Test]
    public async Task StartMyWaveAsync_WhenRadioThrows_HandlesErrorAndReturnsNull()
    {
        var ex = new InvalidOperationException("boom");
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ex);

        var result = await _presenter.StartMyWaveAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            _errorHandler.Verify(e => e.Handle(ex), Times.Once);
        });
    }

    [Test]
    public async Task FetchMoreTracksAsync_WhenRadioThrows_HandlesError()
    {
        var ex = new InvalidOperationException("boom");
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch("a");
        await _presenter.StartMyWaveAsync();

        _radio.Setup(r => r.FetchNextBatchAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ex);
        await _presenter.FetchMoreTracksAsync();

        _errorHandler.Verify(e => e.Handle(ex), Times.Once);
    }

    // ── Уведомления радио ─────────────────────────────────────────────────

    [Test]
    public async Task NotifyTrackStarted_DelegatesToRadioSource()
    {
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch("a");
        await _presenter.StartMyWaveAsync();

        await _presenter.NotifyTrackStartedAsync(K("t1"));

        _radio.Verify(r => r.SendTrackStartedAsync(K("t1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task NotifyTrackFinishedAndSkipped_DelegateToRadioSource()
    {
        _radio.Setup(r => r.StartPersonalRadioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupBatch("a");
        await _presenter.StartMyWaveAsync();

        await _presenter.NotifyTrackFinishedAsync(K("t1"), 12.5);
        await _presenter.NotifyTrackSkippedAsync(K("t2"), 3);

        Assert.Multiple(() =>
        {
            _radio.Verify(r => r.SendTrackFinishedAsync(K("t1"), 12.5, It.IsAny<CancellationToken>()), Times.Once);
            _radio.Verify(r => r.SendTrackSkippedAsync(K("t2"), 3, It.IsAny<CancellationToken>()), Times.Once);
        });
    }
}
