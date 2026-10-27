using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

/// <summary>
/// Выбор пути загрузки: плейлист ядра — стратегия по <c>Kind</c>, временный плейлист источника —
/// буфер хоста, обычный плейлист источника — сам источник.
/// </summary>
[TestFixture]
public sealed class TrackRepositoryTests
{
    private static TrackKey K(string trackId) => new(YandexMusicSourceId.Value, trackId);

    private Mock<IMusicSourceRegistry> _registry = null!;
    private Mock<IMusicSource> _yandexSource = null!;
    private Mock<ITrackRepositoryCache> _cache = null!;
    private Mock<ITrackInfoProvider> _infoProvider = null!;
    private Mock<IAppPlaylistProvider> _appPlaylists = null!;
    private Mock<IPlaylistStateStore> _stateStore = null!;
    private Mock<IPlaylistCompositionStore> _compositionStore = null!;
    private Mock<IPlaylistRefreshStatus> _refreshStatus = null!;
    private Mock<IPlaylistLoadStrategy> _top10Strategy = null!;
    private TrackRepository _repository = null!;
    private PlaylistState? _saved;

    [SetUp]
    public void SetUp()
    {
        _registry = new Mock<IMusicSourceRegistry>();
        _yandexSource = new Mock<IMusicSource>();
        _cache = new Mock<ITrackRepositoryCache>();
        _infoProvider = new Mock<ITrackInfoProvider>();
        _appPlaylists = new Mock<IAppPlaylistProvider>();
        _stateStore = new Mock<IPlaylistStateStore>();
        _compositionStore = new Mock<IPlaylistCompositionStore>();
        _refreshStatus = new Mock<IPlaylistRefreshStatus>();
        _top10Strategy = new Mock<IPlaylistLoadStrategy>();

        _yandexSource.SetupGet(s => s.Id).Returns(YandexMusicSourceId.Value);
        _yandexSource.Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Playlist>());
        _yandexSource.Setup(s => s.GetPlaylistTrackIdsAsync(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        _registry.SetupGet(r => r.Sources).Returns([_yandexSource.Object]);
        _registry.Setup(r => r.GetRequired(YandexMusicSourceId.Value)).Returns(_yandexSource.Object);
        _registry.Setup(r => r.Get(YandexMusicSourceId.Value)).Returns(_yandexSource.Object);

        _cache.SetupGet(c => c.MyWaveTracks).Returns([]);
        _cache.Setup(c => c.QueueTrackIds).Returns([]);

        _appPlaylists.Setup(p => p.GetAppPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Playlist>());

        _stateStore.Setup(s => s.Save(It.IsAny<PlaylistState>()))
            .Callback<PlaylistState>(state => _saved = state);

        _compositionStore
            .Setup(s => s.TryLoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TrackKey>?)null);
        _compositionStore
            .Setup(s => s.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<TrackKey>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _compositionStore
            .Setup(s => s.GetKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _compositionStore
            .Setup(s => s.RemoveAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _top10Strategy.SetupGet(s => s.Kind).Returns("top10");
        _top10Strategy.Setup(s => s.LoadTrackIdsAsync(It.IsAny<Playlist>()))
            .ReturnsAsync([K("1"), K("2")]);

        _repository = CreateRepository(_cache.Object);
    }

    private TrackRepository CreateRepository(ITrackRepositoryCache cache)
        => new(
            _registry.Object,
            _infoProvider.Object,
            new Mock<IHistoryService>().Object,
            cache,
            new Mock<ILocalLibraryService>().Object,
            new PlaylistLoadStrategyResolver([_top10Strategy.Object]),
            _appPlaylists.Object,
            _stateStore.Object,
            _compositionStore.Object,
            new Mock<IEventBus>().Object,
            _refreshStatus.Object);

    [Test]
    public async Task GetPlaylists_PreservesSnapshotForNotReadySource()
    {
        var oldYandex = new Playlist("favorite", "Мои треки")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Data
        };
        _stateStore.Setup(s => s.Load())
            .Returns(new PlaylistState { Playlists = [oldYandex], LastPlaylist = oldYandex });
        _yandexSource.SetupGet(s => s.State).Returns(SourceState.RequiresSetup);

        await _repository.GetPlaylists();

        Assert.Multiple(() =>
        {
            Assert.That(_saved!.Playlists, Does.Contain(oldYandex));
            Assert.That(_saved.LastPlaylist, Is.EqualTo(oldYandex));
        });
    }

    [Test]
    public async Task GetPlaylists_WhenSourceFails_KeepsSnapshotPlaylists()
    {
        // Один неудачный запрос не должен обеднить снимок: иначе следующий запуск
        // покажет меньше, чем было при закрытии.
        var oldYandex = new Playlist("favorite", "Мои треки")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Data
        };
        _stateStore.Setup(s => s.Load())
            .Returns(new PlaylistState { Playlists = [oldYandex], LastPlaylist = oldYandex });
        _yandexSource.SetupGet(s => s.State).Returns(SourceState.Ready);
        _yandexSource.Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("сеть недоступна"));

        var playlists = (await _repository.GetPlaylists()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(playlists.Select(p => p.Title), Does.Contain("Мои треки"));
            Assert.That(_saved!.Playlists.Select(p => p.Title), Does.Contain("Мои треки"));
        });
    }

    [Test]
    public async Task GetStartupSnapshot_DropsPlaylistsOfUnregisteredSource()
    {
        // Плагин удалён/несовместим: его плейлисты не должны висеть в снимке вечно.
        var ghost = new Playlist("section", "ВК")
        {
            SourceId = new SourceId("vk-music"),
            Category = PlaylistCategory.Data
        };
        _stateStore.Setup(s => s.Load())
            .Returns(new PlaylistState { Playlists = [ghost], LastPlaylist = ghost });

        PlaylistStartupSnapshot snapshot = await _repository.GetStartupSnapshotAsync();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Playlists, Is.Empty);
            Assert.That(snapshot.LastPlaylist, Is.Null);
        });
    }

    [Test]
    public async Task SetPlaylist_LoadsCompositionFromStoreWithoutSource()
    {
        Playlist playlist = DataPlaylist();
        _yandexSource.SetupGet(s => s.State).Returns(SourceState.RequiresSetup);
        _compositionStore
            .Setup(s => s.TryLoadAsync(playlist.CacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { K("10") });

        await _repository.SetPlaylist(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.GetAllTrackIds(), Is.EqualTo(new[] { K("10") }));
            _yandexSource.Verify(
                s => s.GetPlaylistTrackIdsAsync(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()),
                Times.Never);
        });
    }

    [Test]
    public async Task GetCachedTracks_AdvancesOffsetSoPaginationDoesNotRepeatFirstPage()
    {
        Playlist playlist = DataPlaylist();
        _yandexSource.SetupGet(s => s.State).Returns(SourceState.RequiresSetup);
        _compositionStore
            .Setup(s => s.TryLoadAsync(playlist.CacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { K("1"), K("2"), K("3") });
        _infoProvider
            .Setup(p => p.GetCachedTracksByIds(It.IsAny<IEnumerable<TrackKey>>()))
            .ReturnsAsync((IEnumerable<TrackKey> keys) => keys.Select(key => Track(key.TrackId)).ToList());
        _infoProvider
            .Setup(p => p.GetTracksInfoByIds(It.IsAny<IEnumerable<TrackKey>>()))
            .ReturnsAsync((IEnumerable<TrackKey> keys) => keys.Select(key => Track(key.TrackId)).ToList());

        await _repository.SetPlaylist(playlist);
        await _repository.GetCachedTracks(2);

        var next = (await _repository.GetNextTracks(2)).ToList();

        Assert.That(next.Select(track => track.Id), Is.EqualTo(new[] { "3" }));
    }

    // ── SetPlaylist: выбор пути ────────────────────────────────────────────

    [Test]
    public async Task SetPlaylist_WithSourceId_DelegatesToSource()
    {
        var playlist = new Playlist("own", "Мой микс")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Data
        };
        _yandexSource.Setup(s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["10", "20"]);

        await _repository.SetPlaylist(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.GetAllTrackIds(), Is.EqualTo(new[] { K("10"), K("20") }));
            _yandexSource.Verify(s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()), Times.Once);
            _top10Strategy.Verify(s => s.LoadTrackIdsAsync(It.IsAny<Playlist>()), Times.Never);
        });
    }

    [Test]
    public async Task SetPlaylist_WithoutSourceId_UsesStrategyByKind()
    {
        var playlist = new Playlist("top10", "Топ 10") { Category = PlaylistCategory.Computed };

        await _repository.SetPlaylist(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.GetAllTrackIds(), Is.EqualTo(new[] { K("1"), K("2") }));
            _top10Strategy.Verify(s => s.LoadTrackIdsAsync(playlist), Times.Once);
            _yandexSource.Verify(
                s => s.GetPlaylistTrackIdsAsync(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()),
                Times.Never);
        });
    }

    [Test]
    public async Task SetPlaylist_RadioTransient_UsesWaveBuffer()
    {
        var playlist = new Playlist("my-wave", "Моя волна")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Transient,
            Context = PlaylistContext.Radio
        };
        _cache.SetupGet(c => c.MyWaveTracks).Returns([Track("w1"), Track("w2")]);

        await _repository.SetPlaylist(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.GetAllTrackIds(), Is.EqualTo(new[] { K("w1"), K("w2") }));
            _yandexSource.Verify(
                s => s.GetPlaylistTrackIdsAsync(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()),
                Times.Never);
        });
    }

    [Test]
    public async Task SetPlaylist_SearchTransient_UsesSearchCache()
    {
        var playlist = new Playlist("search", "Поиск: кино")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Transient
        };
        IReadOnlyList<TrackKey> cached = [K("s1")];
        _cache.Setup(c => c.TryGetSearchTrackIds(YandexMusicSourceId.Value, out cached)).Returns(true);

        await _repository.SetPlaylist(playlist);

        Assert.That(_repository.GetAllTrackIds(), Is.EqualTo(new[] { K("s1") }));
    }

    // ── SetPlaylist: состав плейлиста кешируется на время запуска ──────────

    private static Playlist DataPlaylist()
        => new("own", "Мой микс")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Data,
            Payload = new Dictionary<string, string> { ["owner"] = "42", ["kind"] = "7" }
        };

    [Test]
    public async Task SetPlaylist_SamePlaylistTwice_AsksSourceOnce()
    {
        TrackRepository repository = CreateRepository(new TrackRepositoryCache());
        Playlist playlist = DataPlaylist();
        _yandexSource.Setup(s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["10", "20"]);

        await repository.SetPlaylist(playlist);
        await repository.SetPlaylist(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(repository.GetAllTrackIds(), Is.EqualTo(new[] { K("10"), K("20") }));
            _yandexSource.Verify(
                s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()),
                Times.Once);
        });
    }

    [Test]
    public async Task SetPlaylist_EmptySourceAnswer_IsNotCached()
    {
        TrackRepository repository = CreateRepository(new TrackRepositoryCache());
        Playlist playlist = DataPlaylist();
        _yandexSource.Setup(s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        await repository.SetPlaylist(playlist);
        await repository.SetPlaylist(playlist);

        _yandexSource.Verify(
            s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task GetPlaylists_ClearsCompositionCache()
    {
        TrackRepository repository = CreateRepository(new TrackRepositoryCache());
        Playlist playlist = DataPlaylist();
        _yandexSource.Setup(s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["10"]);

        await repository.SetPlaylist(playlist);
        await repository.GetPlaylists();
        await repository.SetPlaylist(playlist);

        _yandexSource.Verify(
            s => s.GetPlaylistTrackIdsAsync(playlist, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── GetPlaylists: что попадает в дерево и в снимок ─────────────────────

    [Test]
    public async Task GetPlaylists_SkipsDataWithoutSource()
    {
        _yandexSource.Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Playlist("favorite", "Мои треки"),
                new Playlist("own", "Мой микс") { SourceId = YandexMusicSourceId.Value }
            ]);

        var playlists = (await _repository.GetPlaylists()).ToList();

        Assert.That(playlists.Select(p => p.Title), Is.EqualTo(new[] { "Мой микс" }));
    }

    [Test]
    public async Task GetPlaylists_KeepsOnlyDataFromSources()
    {
        var appPlaylist = new Playlist("top10", "Топ 10") { Category = PlaylistCategory.Computed };
        _appPlaylists.Setup(p => p.GetAppPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([appPlaylist]);
        _yandexSource.Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Playlist("favorite", "Мои треки")
                {
                    SourceId = YandexMusicSourceId.Value,
                    Category = PlaylistCategory.Data
                },
                new Playlist("own", "Мой микс")
                {
                    SourceId = YandexMusicSourceId.Value,
                    Category = PlaylistCategory.Computed
                },
                new Playlist("my-wave", "Моя волна")
                {
                    SourceId = YandexMusicSourceId.Value,
                    Category = PlaylistCategory.Transient
                },
                new Playlist("artist", "Кино")
                {
                    SourceId = YandexMusicSourceId.Value,
                    Category = PlaylistCategory.Entity
                }
            ]);

        var playlists = (await _repository.GetPlaylists()).ToList();

        Assert.That(playlists.Select(p => p.Title), Is.EqualTo(new[] { "Мои треки", "Топ 10" }));
    }

    [Test]
    public async Task PersistState_StoresOnlyDataPlaylists()
    {
        _yandexSource.Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Playlist("favorite", "Мои треки") { SourceId = YandexMusicSourceId.Value },
                new Playlist("own", "Мой микс")
                {
                    SourceId = YandexMusicSourceId.Value,
                    Category = PlaylistCategory.Computed
                }
            ]);

        await _repository.GetPlaylists();

        Assert.That(_saved, Is.Not.Null);
        Assert.That(_saved!.Playlists.Select(p => p.Title), Is.EqualTo(new[] { "Мои треки" }));
    }

    [Test]
    public async Task PersistLastPlaylist_IgnoresTransient()
    {
        _yandexSource.Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Playlist("favorite", "Мои треки") { SourceId = YandexMusicSourceId.Value }]);
        await _repository.GetPlaylists();

        var transient = new Playlist("search", "Поиск: кино")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Transient
        };
        await _repository.SetPlaylist(transient);

        Assert.Multiple(() =>
        {
            _stateStore.Verify(s => s.Save(It.IsAny<PlaylistState>()), Times.Once);
            Assert.That(_saved!.LastPlaylist, Is.Null);
        });
    }

    private static Track Track(string id)
        => new($"Title-{id}", $"Artist-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };
}
