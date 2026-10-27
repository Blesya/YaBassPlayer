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

        _repository
            .Setup(r => r.GetStartupSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlaylistStartupSnapshot.Empty);
        _repository
            .Setup(r => r.RefreshPlaylistsAsync(It.IsAny<PlaylistRefreshScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistRefreshResult([], []));
        _composer
            .Setup(c => c.ComposeAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlaylistTreeItem>());

        _presenter = new PlaylistsPresenter(_view.Object, _repository.Object, _composer.Object, _errorHandler.Object);
    }

    private void SetupStartup(params Playlist[] playlists)
    {
        _repository
            .Setup(r => r.GetStartupSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistStartupSnapshot(playlists, null, null, DateTime.UtcNow));
        SetupCompose(playlists);
    }

    private void SetupCompose(params Playlist[] playlists)
    {
        var roots = playlists.Select(PlaylistTreeItem.FromPlaylist).ToList();
        _composer
            .Setup(c => c.ComposeAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roots);
    }

    private void SetupRefresh(params Playlist[] playlists)
    {
        _repository
            .Setup(r => r.RefreshPlaylistsAsync(It.IsAny<PlaylistRefreshScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistRefreshResult(playlists, []));
    }

    private async Task AwaitRefreshAsync()
    {
        if (_presenter.PendingRefresh is { } pending)
            await pending;
    }

    // ── Инициализация дерева ──────────────────────────────────────────────

    [Test]
    public async Task InitializeAsync_PushesSnapshotTreeToViewWithoutNetwork()
    {
        var playlist = new Playlist("favorite", "Мои треки");
        SetupStartup(playlist);

        await _presenter.InitializeAsync();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetPlaylistTree(It.IsAny<IEnumerable<PlaylistTreeItem>>()), Times.Once);
            _view.Verify(v => v.MarkAsPlaying(playlist), Times.Once);
            _composer.Verify(c => c.InvalidateCache(), Times.Once);
        });
    }

    [Test]
    public async Task InitializeAsync_SelectsFirstPlaylistWhenNoLastPlaylist()
    {
        var playlist = new Playlist("favorite", "Мои треки");
        SetupStartup(playlist);

        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        await _presenter.InitializeAsync();

        Assert.That(chosen, Is.SameAs(playlist));
    }

    [Test]
    public async Task InitializeAsync_WhenNoPlaylists_DoesNotMarkAnythingPlaying()
    {
        await _presenter.InitializeAsync();

        _view.Verify(v => v.MarkAsPlaying(It.IsAny<Playlist>()), Times.Never);
    }

    [Test]
    public async Task InitializeAsync_WhenRepositoryThrows_HandlesError()
    {
        var ex = new InvalidOperationException("boom");
        _repository
            .Setup(r => r.GetStartupSnapshotAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);

        await _presenter.InitializeAsync();

        _errorHandler.Verify(e => e.Handle(ex), Times.Once);
    }

    // ── Запуск из сохранённого состояния ──────────────────────────────────

    [Test]
    public async Task InitializeAsync_WhenSnapshotHasLastPlaylist_RestoresIt()
    {
        var first = new Playlist("favorite", "A");
        var last = new Playlist("personal", "B") { SourceId = YandexMusicSourceId.Value };
        _repository
            .Setup(r => r.GetStartupSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistStartupSnapshot([first, last], last, null, DateTime.UtcNow));
        SetupCompose(first, last);

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
    public async Task InitializeAsync_WhenLastPlaylistIsNotInTree_SelectsFirstAvailable()
    {
        // Прошлый плейлист исчез (например, источник его больше не отдаёт) —
        // приложение не должно остаться с пустым списком.
        var favorite = new Playlist("favorite", "Мои треки") { SourceId = YandexMusicSourceId.Value };
        var missing = new Playlist("personal", "Пропавший") { SourceId = YandexMusicSourceId.Value };
        _repository
            .Setup(r => r.GetStartupSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistStartupSnapshot([favorite], missing, null, DateTime.UtcNow));
        SetupCompose(favorite);

        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        await _presenter.InitializeAsync();

        Assert.That(chosen, Is.SameAs(favorite));
    }

    [Test]
    public async Task InitializeAsync_RestoresLastPlaylistAfterBackgroundRefresh()
    {
        // «Загруженные» (Computed) не попадают в снимок, но указатель LastPlaylist
        // на них сохраняется: после фонового обновления должен быть выбран именно он.
        var favorite = new Playlist("favorite", "Мои треки") { SourceId = YandexMusicSourceId.Value };
        var cached = new Playlist("cached", "Загруженные") { Category = PlaylistCategory.Computed };
        _repository
            .Setup(r => r.GetStartupSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistStartupSnapshot([favorite], cached, null, DateTime.UtcNow));
        _composer
            .SetupSequence(c => c.ComposeAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PlaylistTreeItem.FromPlaylist(favorite)])
            .ReturnsAsync([PlaylistTreeItem.FromPlaylist(favorite), PlaylistTreeItem.FromPlaylist(cached)]);
        SetupRefresh(favorite, cached);

        Playlist? chosen = null;
        _presenter.PlaylistChosen += p => chosen = p;

        await _presenter.InitializeAsync();
        await AwaitRefreshAsync();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.MarkAsPlaying(cached), Times.Once);
            Assert.That(chosen, Is.SameAs(cached));
        });
    }

    // ── Фоновое обновление ────────────────────────────────────────────────

    [Test]
    public async Task InitializeAsync_StartsFullBackgroundRefresh()
    {
        SetupStartup(new Playlist("favorite", "Мои треки"));

        await _presenter.InitializeAsync();
        await AwaitRefreshAsync();

        _repository.Verify(
            r => r.RefreshPlaylistsAsync(PlaylistRefreshScope.All, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task LoadPlaylistTree_RequestsFullRefreshWithStaticCacheInvalidation()
    {
        SetupRefresh(new Playlist("favorite", "Мои треки"));
        SetupCompose(new Playlist("favorite", "Мои треки"));

        _presenter.LoadPlaylistTree();
        await AwaitRefreshAsync();

        Assert.Multiple(() =>
        {
            _repository.Verify(
                r => r.RefreshPlaylistsAsync(PlaylistRefreshScope.All, It.IsAny<CancellationToken>()),
                Times.Once);
            _composer.Verify(c => c.InvalidateCache(), Times.Once);
        });
    }

    [Test]
    public async Task RefreshAppPlaylists_RequestsOnlyLocalPlaylists()
    {
        SetupRefresh(new Playlist("top10", "Топ 10") { Category = PlaylistCategory.Computed });
        SetupCompose(new Playlist("top10", "Топ 10") { Category = PlaylistCategory.Computed });

        _presenter.RefreshAppPlaylists();
        await AwaitRefreshAsync();

        Assert.Multiple(() =>
        {
            _repository.Verify(
                r => r.RefreshPlaylistsAsync(PlaylistRefreshScope.AppPlaylists, It.IsAny<CancellationToken>()),
                Times.Once);
            _composer.Verify(c => c.InvalidateCache(), Times.Never);
        });
    }

    [Test]
    public async Task Refresh_DoesNotRaiseChosenAgainForSamePlaylist()
    {
        var playlist = new Playlist("favorite", "Мои треки");
        SetupStartup(playlist);
        SetupRefresh(playlist);
        SetupCompose(playlist);

        var chosen = 0;
        _presenter.PlaylistChosen += _ => chosen++;

        await _presenter.InitializeAsync();
        await AwaitRefreshAsync();

        Assert.That(chosen, Is.EqualTo(1));
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

    [Test]
    public void OnPlaylistSelected_SamePlaylistTwice_RaisesChosenOnce()
    {
        var playlist = new Playlist("personal", "P");
        var chosen = 0;
        _presenter.PlaylistChosen += _ => chosen++;

        _view.Raise(v => v.PlaylistSelected += null, playlist);
        _view.Raise(v => v.PlaylistSelected += null, playlist);

        Assert.That(chosen, Is.EqualTo(1));
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
