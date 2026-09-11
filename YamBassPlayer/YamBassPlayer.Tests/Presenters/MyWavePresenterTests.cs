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
    private Mock<IYandexRadioService> _radio = null!;
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
        _radio = new Mock<IYandexRadioService>();
        _trackInfoProvider = new Mock<ITrackInfoProvider>();
        _cache = new Mock<ITrackRepositoryCache>();
        _tracksPresenter = new Mock<ITracksPresenter>();
        _queue = new Mock<IPlaybackQueue>();
        _playStatus = new Mock<IPlayStatusPresenter>();
        _errorHandler = new Mock<IErrorHandler>();

        _tracksPresenter.Setup(t => t.LoadTracksFor(It.IsAny<Playlist>())).Returns(Task.CompletedTask);

        _presenter = new MyWavePresenter(
            _radio.Object, _trackInfoProvider.Object, _cache.Object,
            _tracksPresenter.Object, _queue.Object, _playStatus.Object, _errorHandler.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);

    private void SetupBatch(params string[] ids)
    {
        IReadOnlyList<string> trackIds = ids;
        IReadOnlyList<Track> tracks = ids.Select(Track).ToList();
        _radio.Setup(r => r.FetchNextBatchAsync()).ReturnsAsync((trackIds, tracks));
    }

    // ── Запуск волны ──────────────────────────────────────────────────────

    [Test]
    public async Task StartMyWaveAsync_WhenStationFails_ReturnsNullAndReports()
    {
        _radio.Setup(r => r.StartMyWaveAsync()).ReturnsAsync(false);

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
        _radio.Setup(r => r.StartMyWaveAsync()).ReturnsAsync(true);
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
        _radio.Setup(r => r.StartMyWaveAsync()).ReturnsAsync(true);
        SetupBatch("a", "b");

        var result = await _presenter.StartMyWaveAsync();

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            _trackInfoProvider.Verify(p => p.SaveAsync(It.IsAny<Track>()), Times.Exactly(2));
            _cache.Verify(c => c.ReplaceMyWaveTracks(It.IsAny<IEnumerable<Track>>()), Times.Once);
            _tracksPresenter.Verify(t => t.LoadTracksFor(result!), Times.Once);
            _queue.Verify(q => q.SetQueue(It.Is<IEnumerable<string>>(x => x.SequenceEqual(new[] { "a", "b" })), 0), Times.Once);
        });
        Assert.That(result!.Type, Is.EqualTo(PlaylistType.MyWave));
    }

    [Test]
    public async Task StartMyWaveFromTrackAsync_UsesTrackWaveAndSeedDescription()
    {
        _trackInfoProvider.Setup(p => p.GetTrackInfoById("seed")).ReturnsAsync(Track("seed"));
        _radio.Setup(r => r.StartTrackWaveAsync("seed")).ReturnsAsync(true);
        SetupBatch("x");

        var result = await _presenter.StartMyWaveFromTrackAsync("seed");

        Assert.Multiple(() =>
        {
            _radio.Verify(r => r.StartTrackWaveAsync("seed"), Times.Once);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Description, Does.Contain("A-seed"));
        });
    }

    // ── Догрузка батча ────────────────────────────────────────────────────

    [Test]
    public async Task FetchMoreTracksAsync_AppendsToCacheAndQueue()
    {
        SetupBatch("c", "d");

        await _presenter.FetchMoreTracksAsync();

        Assert.Multiple(() =>
        {
            _cache.Verify(c => c.AppendMyWaveTracks(It.IsAny<IEnumerable<Track>>()), Times.Once);
            _queue.Verify(q => q.AddToQueue(It.Is<IEnumerable<string>>(x => x.SequenceEqual(new[] { "c", "d" }))), Times.Once);
        });
    }

    [Test]
    public async Task FetchMoreTracksAsync_WhenBatchEmpty_DoesNothing()
    {
        SetupBatch();

        await _presenter.FetchMoreTracksAsync();

        _cache.Verify(c => c.AppendMyWaveTracks(It.IsAny<IEnumerable<Track>>()), Times.Never);
    }

    // ── Обработка ошибок ──────────────────────────────────────────────────

    [Test]
    public async Task StartMyWaveAsync_WhenRadioThrows_HandlesErrorAndReturnsNull()
    {
        var ex = new InvalidOperationException("boom");
        _radio.Setup(r => r.StartMyWaveAsync()).ThrowsAsync(ex);

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
        _radio.Setup(r => r.FetchNextBatchAsync()).ThrowsAsync(ex);

        await _presenter.FetchMoreTracksAsync();

        _errorHandler.Verify(e => e.Handle(ex), Times.Once);
    }

    // ── Уведомления радио ─────────────────────────────────────────────────

    [Test]
    public async Task NotifyTrackStarted_DelegatesToRadioService()
    {
        await _presenter.NotifyTrackStartedAsync("t1");

        _radio.Verify(r => r.SendTrackStartedAsync("t1"), Times.Once);
    }

    [Test]
    public async Task NotifyTrackFinishedAndSkipped_DelegateToRadioService()
    {
        await _presenter.NotifyTrackFinishedAsync("t1", 12.5);
        await _presenter.NotifyTrackSkippedAsync("t2", 3);

        Assert.Multiple(() =>
        {
            _radio.Verify(r => r.SendTrackFinishedAsync("t1", 12.5), Times.Once);
            _radio.Verify(r => r.SendTrackSkippedAsync("t2", 3), Times.Once);
        });
    }
}
