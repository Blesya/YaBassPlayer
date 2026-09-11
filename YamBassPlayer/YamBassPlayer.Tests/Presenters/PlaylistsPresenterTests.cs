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
        var playlist = new Playlist("Мои треки", PlaylistType.Favorite);
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
        var playlist = new Playlist("Мои треки", PlaylistType.Favorite);
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
        SetupTree(new Playlist("P", PlaylistType.Favorite));

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
        var playlist = new Playlist("Мои треки", PlaylistType.Favorite);
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
        var first = new Playlist("A", PlaylistType.Favorite);
        var last = new Playlist("B", PlaylistType.Custom);
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
        var playlist = new Playlist("Мои треки", PlaylistType.Favorite);
        SetupTree(playlist);
        _repository.Setup(r => r.GetPersistedSnapshot()).Returns(new PlaylistState());

        await _presenter.InitializeAsync();

        _repository.Verify(r => r.GetPlaylists(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Выбор плейлиста ───────────────────────────────────────────────────

    [Test]
    public void OnPlaylistSelected_MarksPlayingAndRaisesChosen()
    {
        var playlist = new Playlist("P", PlaylistType.Custom);
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
        var playlist = new Playlist("Поиск: кино", PlaylistType.YandexSearch);

        _presenter.NotifyTransientPlaylistActive(playlist);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.AddOrUpdateTransientPlaylist(playlist), Times.Once);
            _view.Verify(v => v.MarkAsPlaying(playlist), Times.Once);
        });
    }
}
