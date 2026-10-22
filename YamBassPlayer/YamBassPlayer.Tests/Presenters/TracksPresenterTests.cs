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
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private Mock<ITracksView> _view = null!;
    private Mock<ITrackFileProvider> _fileProvider = null!;
    private Mock<ITrackRepository> _repository = null!;
    private Mock<IPlaybackQueue> _queue = null!;
    private Mock<ITrackDownloadTracker> _downloadTracker = null!;
    private EventBus _eventBus = null!;
    private TracksPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _view = new Mock<ITracksView>();
        _fileProvider = new Mock<ITrackFileProvider>();
        _repository = new Mock<ITrackRepository>();
        _queue = new Mock<IPlaybackQueue>();
        _downloadTracker = new Mock<ITrackDownloadTracker>();
        _downloadTracker.Setup(t => t.GetActive()).Returns([]);
        _eventBus = new EventBus();

        _presenter = new TracksPresenter(
            _view.Object, _fileProvider.Object, _repository.Object, _queue.Object, _eventBus, _downloadTracker.Object);
    }

    private static Track Track(string id) => new($"Title-{id}", $"Artist-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };

    // ── Прогресс загрузки ─────────────────────────────────────────────────

    [Test]
    public void DownloadTracker_Changed_ForwardsStateToView()
    {
        var state = new TrackDownloadState(TrackDownloadStatus.Downloading, 45, 450, true);

        _downloadTracker.Raise(t => t.Changed += null, K("t1"), state);

        _view.Verify(v => v.SetDownloadState(K("t1"), state), Times.Once);
    }

    [Test]
    public async Task LoadTracksFor_ReplaysActiveDownloadsIntoFreshList()
    {
        var playlist = new Playlist("favorite", "P");
        var tracks = new[] { Track("a") };
        var active = new TrackDownloadState(TrackDownloadStatus.Downloading, 20, 200, true);
        _repository.Setup(r => r.SetPlaylist(playlist, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repository.Setup(r => r.GetCachedTracksOrMinimum(100, It.IsAny<CancellationToken>())).ReturnsAsync(tracks);
        _downloadTracker.Setup(t => t.GetActive()).Returns([(K("a"), active)]);

        await _presenter.LoadTracksFor(playlist);

        _view.Verify(v => v.SetDownloadState(K("a"), active), Times.Once);
    }

    // ── Загрузка плейлиста ────────────────────────────────────────────────

    [Test]
    public async Task LoadTracksFor_LoadsCachedBatchAndPushesToView()
    {
        var playlist = new Playlist("favorite", "P");
        var tracks = new[] { Track("a"), Track("b") };
        _repository.Setup(r => r.SetPlaylist(playlist, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repository.Setup(r => r.GetCachedTracksOrMinimum(100, It.IsAny<CancellationToken>())).ReturnsAsync(tracks);
        _queue.SetupGet(q => q.CurrentTrack).Returns(K("a"));

        await _presenter.LoadTracksFor(playlist);

        Assert.Multiple(() =>
        {
            _repository.Verify(r => r.SetPlaylist(playlist, It.IsAny<CancellationToken>()), Times.Once);
            _view.Verify(
                v => v.SetTracks(It.Is<IEnumerable<Track>>(t => t.SequenceEqual(tracks)), It.IsAny<Func<Track, bool>>()),
                Times.Once);
            _view.Verify(v => v.SetPlayingTrack(K("a")), Times.Once);
        });
    }

    [Test]
    public async Task LoadTracksFor_WhenNoTracks_ClearsView()
    {
        var playlist = new Playlist("personal", "P");
        _repository.Setup(r => r.GetCachedTracksOrMinimum(100, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _presenter.LoadTracksFor(playlist);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ClearTracks(), Times.Once);
            _view.Verify(v => v.SetTracks(It.IsAny<IEnumerable<Track>>(), It.IsAny<Func<Track, bool>>()), Times.Never);
        });
    }

    // ── Активация ячейки → очередь ────────────────────────────────────────

    [Test]
    public async Task OnCellActivated_SetsQueueAtGivenIndex()
    {
        await LoadThreeTracks();
        var keys = new[] { K("a"), K("b"), K("c") };
        _repository.Setup(r => r.GetAllTrackIds()).Returns(keys);

        _view.Raise(v => v.OnCellActivated += null, 2);

        _queue.Verify(q => q.SetQueue(It.Is<IEnumerable<TrackKey>>(x => x.SequenceEqual(keys)), 2), Times.Once);
    }

    [Test]
    public async Task OnCellActivated_OutOfRange_DoesNotSetQueue()
    {
        await LoadThreeTracks();
        _repository.Setup(r => r.GetAllTrackIds()).Returns(new[] { K("a") });

        _view.Raise(v => v.OnCellActivated += null, 5);

        _queue.Verify(q => q.SetQueue(It.IsAny<IEnumerable<TrackKey>>(), It.IsAny<int>()), Times.Never);
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
            v => v.AddTracks(It.Is<IEnumerable<Track>>(t => t.SequenceEqual(more)), It.IsAny<Func<Track, bool>>()),
            Times.Once);
    }

    [Test]
    public async Task NeedMoreTracks_WhenNoMore_DoesNotAppend()
    {
        await LoadThreeTracks();
        _repository.Setup(r => r.GetNextTracks(100, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _view.Raise(v => v.NeedMoreTracks += null);

        _view.Verify(v => v.AddTracks(It.IsAny<IEnumerable<Track>>(), It.IsAny<Func<Track, bool>>()), Times.Never);
    }

    // ── Смена играющего трека через шину событий ──────────────────────────

    [Test]
    public void TrackChangedEvent_UpdatesPlayingTrackInView()
    {
        _eventBus.Publish(new TrackChangedEvent(K("z")));

        _view.Verify(v => v.SetPlayingTrack(K("z")), Times.Once);
    }

    [Test]
    public void TrackChangedEvent_RemembersLastPlayedTrack()
    {
        _eventBus.Publish(new TrackChangedEvent(K("z")));

        _repository.Verify(r => r.SetLastPlayedTrack(K("z")), Times.Once);
    }

    // ── Восстановление выделения ──────────────────────────────────────────

    [Test]
    public async Task LoadTracksFor_RestoresLastPlayedSelection()
    {
        _repository.Setup(r => r.GetPersistedSnapshot()).Returns(new PlaylistState { LastTrack = K("b") });

        await LoadThreeTracks();

        _view.Verify(v => v.SelectTrack(K("b")), Times.Once);
    }

    [Test]
    public async Task LoadTracksFor_RestoresSelectionAfterBackgroundRefresh()
    {
        // Снимок показали → фон обновил плейлисты и перезагрузил список.
        _repository.Setup(r => r.GetPersistedSnapshot()).Returns(new PlaylistState { LastTrack = K("b") });

        await LoadThreeTracks();
        await LoadThreeTracks();

        _view.Verify(v => v.SelectTrack(K("b")), Times.Exactly(2), "Выделение не должно сбрасываться обновлением");
    }

    [Test]
    public async Task LoadTracksFor_KeepsUserSelectionAcrossReload()
    {
        _repository.Setup(r => r.GetPersistedSnapshot()).Returns(new PlaylistState { LastTrack = K("b") });
        await LoadThreeTracks();

        // Пользователь выделил другой трек.
        _view.Raise(v => v.OnTrackSelected += null, 2);
        await LoadThreeTracks();

        _view.Verify(v => v.SelectTrack(K("c")), Times.Once);
    }

    [Test]
    public async Task LoadTracksFor_WithoutLastTrack_DoesNotSelect()
    {
        _repository.Setup(r => r.GetPersistedSnapshot()).Returns((PlaylistState?)null);

        await LoadThreeTracks();

        _view.Verify(v => v.SelectTrack(It.IsAny<TrackKey>()), Times.Never);
    }

    [Test]
    public void PlaybackQueue_IsExposedAsProperty()
    {
        Assert.That(_presenter.PlaybackQueue, Is.SameAs(_queue.Object));
    }

    private async Task LoadThreeTracks()
    {
        var playlist = new Playlist("favorite", "P");
        _repository.Setup(r => r.GetCachedTracksOrMinimum(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Track("a"), Track("b"), Track("c") });
        await _presenter.LoadTracksFor(playlist);
    }
}
