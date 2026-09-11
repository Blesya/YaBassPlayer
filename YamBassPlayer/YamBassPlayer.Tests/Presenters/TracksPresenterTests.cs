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
public sealed class TracksPresenterTests
{
    private Mock<ITracksView> _view = null!;
    private Mock<ITrackFileProvider> _fileProvider = null!;
    private Mock<ITrackRepository> _repository = null!;
    private Mock<IPlaybackQueue> _queue = null!;
    private EventBus _eventBus = null!;
    private TracksPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _view = new Mock<ITracksView>();
        _fileProvider = new Mock<ITrackFileProvider>();
        _repository = new Mock<ITrackRepository>();
        _queue = new Mock<IPlaybackQueue>();
        _eventBus = new EventBus();

        _presenter = new TracksPresenter(
            _view.Object, _fileProvider.Object, _repository.Object, _queue.Object, _eventBus);
    }

    private static Track Track(string id) => new($"Title-{id}", $"Artist-{id}", "Album", id);

    // ── Загрузка плейлиста ────────────────────────────────────────────────

    [Test]
    public async Task LoadTracksFor_LoadsCachedBatchAndPushesToView()
    {
        var playlist = new Playlist("P", PlaylistType.Favorite);
        var tracks = new[] { Track("a"), Track("b") };
        _repository.Setup(r => r.SetPlaylist(playlist, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repository.Setup(r => r.GetCachedTracksOrMinimum(100, It.IsAny<CancellationToken>())).ReturnsAsync(tracks);
        _queue.SetupGet(q => q.CurrentTrackId).Returns("a");

        await _presenter.LoadTracksFor(playlist);

        Assert.Multiple(() =>
        {
            _repository.Verify(r => r.SetPlaylist(playlist, It.IsAny<CancellationToken>()), Times.Once);
            _view.Verify(
                v => v.SetTracks(It.Is<IEnumerable<Track>>(t => t.SequenceEqual(tracks)), It.IsAny<Func<string, bool>>()),
                Times.Once);
            _view.Verify(v => v.SetPlayingTrackId("a"), Times.Once);
        });
    }

    [Test]
    public async Task LoadTracksFor_WhenNoTracks_ClearsView()
    {
        var playlist = new Playlist("P", PlaylistType.Custom);
        _repository.Setup(r => r.GetCachedTracksOrMinimum(100, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _presenter.LoadTracksFor(playlist);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ClearTracks(), Times.Once);
            _view.Verify(v => v.SetTracks(It.IsAny<IEnumerable<Track>>(), It.IsAny<Func<string, bool>>()), Times.Never);
        });
    }

    // ── Активация ячейки → очередь ────────────────────────────────────────

    [Test]
    public async Task OnCellActivated_SetsQueueAtGivenIndex()
    {
        await LoadThreeTracks();
        var ids = new[] { "a", "b", "c" };
        _repository.Setup(r => r.GetAllTrackIds()).Returns(ids);

        _view.Raise(v => v.OnCellActivated += null, 2);

        _queue.Verify(q => q.SetQueue(It.Is<IEnumerable<string>>(x => x.SequenceEqual(ids)), 2), Times.Once);
    }

    [Test]
    public async Task OnCellActivated_OutOfRange_DoesNotSetQueue()
    {
        await LoadThreeTracks();
        _repository.Setup(r => r.GetAllTrackIds()).Returns(new[] { "a" });

        _view.Raise(v => v.OnCellActivated += null, 5);

        _queue.Verify(q => q.SetQueue(It.IsAny<IEnumerable<string>>(), It.IsAny<int>()), Times.Never);
    }

    // ── Выбор трека ───────────────────────────────────────────────────────

    [Test]
    public async Task OnTrackSelected_RaisesOnTrackChosenWithLoadedTrack()
    {
        await LoadThreeTracks();

        Track? chosen = null;
        _presenter.OnTrackChosen += t => chosen = t;

        _view.Raise(v => v.OnTrackSelected += null, 1);

        Assert.That(chosen, Is.Not.Null);
        Assert.That(chosen!.Id, Is.EqualTo("b"));
    }

    // ── Пагинация ─────────────────────────────────────────────────────────

    [Test]
    public async Task NeedMoreTracks_AppendsNextBatch()
    {
        await LoadThreeTracks();
        var more = new[] { Track("c"), Track("d") };
        _repository.Setup(r => r.GetNextTracks(100, It.IsAny<CancellationToken>())).ReturnsAsync(more);

        _view.Raise(v => v.NeedMoreTracks += null);

        _view.Verify(
            v => v.AddTracks(It.Is<IEnumerable<Track>>(t => t.SequenceEqual(more)), It.IsAny<Func<string, bool>>()),
            Times.Once);
    }

    [Test]
    public async Task NeedMoreTracks_WhenNoMore_DoesNotAppend()
    {
        await LoadThreeTracks();
        _repository.Setup(r => r.GetNextTracks(100, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _view.Raise(v => v.NeedMoreTracks += null);

        _view.Verify(v => v.AddTracks(It.IsAny<IEnumerable<Track>>(), It.IsAny<Func<string, bool>>()), Times.Never);
    }

    // ── Смена играющего трека через шину событий ──────────────────────────

    [Test]
    public void TrackChangedEvent_UpdatesPlayingTrackInView()
    {
        _eventBus.Publish(new TrackChangedEvent("z"));

        _view.Verify(v => v.SetPlayingTrackId("z"), Times.Once);
    }

    [Test]
    public void PlaybackQueue_IsExposedAsProperty()
    {
        Assert.That(_presenter.PlaybackQueue, Is.SameAs(_queue.Object));
    }

    private async Task LoadThreeTracks()
    {
        var playlist = new Playlist("P", PlaylistType.Favorite);
        _repository.Setup(r => r.GetCachedTracksOrMinimum(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Track("a"), Track("b"), Track("c") });
        await _presenter.LoadTracksFor(playlist);
    }
}
