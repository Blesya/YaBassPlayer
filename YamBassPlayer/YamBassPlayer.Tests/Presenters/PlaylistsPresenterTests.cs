using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class PlaylistsPresenterTests
{
    private Mock<IPlaylistsView> _view = null!;
    private Mock<ITrackRepository> _repository = null!;
    private Mock<IPlaylistTreeComposer> _composer = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private PlaylistsPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _view = new Mock<IPlaylistsView>();
        _repository = new Mock<ITrackRepository>();
        _composer = new Mock<IPlaylistTreeComposer>();
        _errorHandler = new Mock<IErrorHandler>();

        _presenter = new PlaylistsPresenter(_view.Object, _repository.Object, _composer.Object, _errorHandler.Object);
    }

    private void SetupTree(params Playlist[] playlists)
    {
        var roots = playlists.Select(PlaylistTreeItem.FromPlaylist).ToList();
        _repository.Setup(r => r.GetPlaylists(It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlists);
        _composer.Setup(c => c.ComposeAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roots);
    }

    // ── Инициализация дерева ──────────────────────────────────────────────

    [Test]
    public async Task InitializeAsync_PushesTreeToViewAndInvalidatesCache()
    {
        var playlist = new Playlist("favorite", "Мои треки");
        SetupTree(playlist);

        await _presenter.InitializeAsync();

        Assert.Multiple(() =>
        {
            _composer.Verify(c => c.InvalidateCache(), Times.Once);
            _view.Verify(v => v.SetPlaylistTree(It.IsAny<IEnumerable<PlaylistTreeItem>>()), Times.Once);
        });
    }

    [Test]
    public async Task InitializeAsync_MarksFirstPlaylistAsPlayingAndRaisesChosen()
    {
        var playlist = new Playlist("favorite", "Мои треки");
        SetupTree(playlist);

        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        await _presenter.InitializeAsync();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.MarkAsPlaying(playlist), Times.Once);
            Assert.That(chosen, Is.SameAs(playlist));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenNoPlaylists_DoesNotMarkAnythingPlaying()
    {
        SetupTree();

        await _presenter.InitializeAsync();

        _view.Verify(v => v.MarkAsPlaying(It.IsAny<Playlist>()), Times.Never);
    }

    [Test]
    public void LoadPlaylistTree_TriggersInitialization()
    {
        SetupTree(new Playlist("favorite", "P"));

        _presenter.LoadPlaylistTree();

        _repository.Verify(r => r.GetPlaylists(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WhenRepositoryThrows_HandlesError()
    {
        var ex = new InvalidOperationException("boom");
        _repository.Setup(r => r.GetPlaylists(It.IsAny<CancellationToken>())).ThrowsAsync(ex);

        await _presenter.InitializeAsync();

        _errorHandler.Verify(e => e.Handle(ex), Times.Once);
    }

    // ── Запуск из сохранённого состояния ──────────────────────────────────

    [Test]
    public async Task InitializeAsync_WhenPersistedSnapshotExists_UsesItImmediately()
    {
        var playlist = new Playlist("favorite", "Мои треки");
        _repository.Setup(r => r.GetPersistedSnapshot())
            .Returns(new PlaylistState { Playlists = [playlist] });
        _repository.Setup(r => r.GetPlaylists(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Playlist>());
        _composer.Setup(c => c.ComposeAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PlaylistTreeItem.FromPlaylist(playlist) });

        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        await _presenter.InitializeAsync();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetPlaylistTree(It.IsAny<IEnumerable<PlaylistTreeItem>>()), Times.Once);
            _view.Verify(v => v.MarkAsPlaying(playlist), Times.Once);
            Assert.That(chosen, Is.SameAs(playlist));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenSnapshotHasLastPlaylist_RestoresIt()
    {
        var first = new Playlist("favorite", "A");
        var last = new Playlist("personal", "B");
        _repository.Setup(r => r.GetPersistedSnapshot())
            .Returns(new PlaylistState { Playlists = [first, last], LastPlaylist = last });
        _repository.Setup(r => r.GetPlaylists(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Playlist>());
        _composer.Setup(c => c.ComposeAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PlaylistTreeItem.FromPlaylist(first), PlaylistTreeItem.FromPlaylist(last) });

        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        await _presenter.InitializeAsync();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.MarkAsPlaying(last), Times.Once);
            Assert.That(chosen, Is.SameAs(last));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenSnapshotEmpty_FallsBackToRepository()
    {
        var playlist = new Playlist("favorite", "Мои треки");
        SetupTree(playlist);
        _repository.Setup(r => r.GetPersistedSnapshot()).Returns(new PlaylistState());

        await _presenter.InitializeAsync();

        _repository.Verify(r => r.GetPlaylists(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RestoresLastPlaylistThatIsNotInSnapshot()
    {
        // «Загруженные» (Computed) не попадают в снимок, но указатель LastPlaylist на них сохраняется:
        // после фонового обновления дерева должен быть выбран именно он.
        var favorite = new Playlist("favorite", "Мои треки") { SourceId = YandexMusicSourceId.Value };
        var cached = new Playlist("cached", "Загруженные") { Category = PlaylistCategory.Computed };
        _repository.Setup(r => r.GetPersistedSnapshot())
            .Returns(new PlaylistState { Playlists = [favorite], LastPlaylist = cached });
        _repository.Setup(r => r.GetPlaylists(It.IsAny<CancellationToken>()))
            .ReturnsAsync([favorite, cached]);
        _composer.SetupSequence(c => c.ComposeAsync(
                It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PlaylistTreeItem.FromPlaylist(favorite)])
            .ReturnsAsync([PlaylistTreeItem.FromPlaylist(favorite), PlaylistTreeItem.FromPlaylist(cached)]);

        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        await _presenter.InitializeAsync();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.MarkAsPlaying(cached), Times.Once);
            Assert.That(chosen, Is.SameAs(cached));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenLastPlaylistMissing_DoesNotSelectAnother()
    {
        var favorite = new Playlist("favorite", "Мои треки") { SourceId = YandexMusicSourceId.Value };
        var last = new Playlist("personal", "Личный") { SourceId = YandexMusicSourceId.Value };
        _repository.Setup(r => r.GetPersistedSnapshot())
            .Returns(new PlaylistState { Playlists = [favorite], LastPlaylist = last });
        _repository.Setup(r => r.GetPlaylists(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Playlist>());
        _composer.Setup(c => c.ComposeAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PlaylistTreeItem.FromPlaylist(favorite) });

        await _presenter.InitializeAsync();

        // Источник ещё не готов: прошлый плейлист не найден, но и первый попавшийся не выбираем.
        _view.Verify(v => v.MarkAsPlaying(It.IsAny<Playlist>()), Times.Never);
    }

    // ── Выбор плейлиста ───────────────────────────────────────────────────

    [Test]
    public void OnPlaylistSelected_MarksPlayingAndRaisesChosen()
    {
        var playlist = new Playlist("personal", "P");
        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        _view.Raise(v => v.PlaylistSelected += null, playlist);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.MarkAsPlaying(playlist), Times.Once);
            Assert.That(chosen, Is.SameAs(playlist));
        });
    }

    // ── Временный плейлист (результаты поиска) ────────────────────────────

    [Test]
    public void NotifyTransientPlaylistActive_AddsAndMarksPlaying()
    {
        var playlist = new Playlist("search", "Поиск: кино");

        _presenter.NotifyTransientPlaylistActive(playlist);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.AddOrUpdateTransientPlaylist(playlist), Times.Once);
            _view.Verify(v => v.MarkAsPlaying(playlist), Times.Once);
        });
    }
}
