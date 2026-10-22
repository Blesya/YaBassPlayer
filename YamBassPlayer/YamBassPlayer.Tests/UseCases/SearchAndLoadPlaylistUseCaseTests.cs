using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.UseCases;

namespace YamBassPlayer.Tests.UseCases;

[TestFixture]
public sealed class SearchAndLoadPlaylistUseCaseTests
{
    private Mock<ITrackInfoProvider> _trackInfoProvider = null!;
    private Mock<ITrackRepositoryCache> _cache = null!;
    private Mock<ITrackRepository> _repository = null!;
    private Mock<ITracksPresenter> _tracksPresenter = null!;
    private Mock<IPlaylistsPresenter> _playlistsPresenter = null!;
    private Mock<IPlayStatusPresenter> _playStatus = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private SearchAndLoadPlaylistUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _trackInfoProvider = new Mock<ITrackInfoProvider>();
        _cache = new Mock<ITrackRepositoryCache>();
        _repository = new Mock<ITrackRepository>();
        _tracksPresenter = new Mock<ITracksPresenter>();
        _playlistsPresenter = new Mock<IPlaylistsPresenter>();
        _playStatus = new Mock<IPlayStatusPresenter>();
        _errorHandler = new Mock<IErrorHandler>();

        _trackInfoProvider.Setup(p => p.SaveAsync(It.IsAny<Track>())).Returns(Task.CompletedTask);
        _repository.Setup(r => r.SetPlaylist(It.IsAny<Playlist>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tracksPresenter.Setup(t => t.LoadTracksFor(It.IsAny<Playlist>())).Returns(Task.CompletedTask);

        _useCase = new SearchAndLoadPlaylistUseCase(
            _trackInfoProvider.Object,
            _cache.Object,
            _repository.Object,
            _tracksPresenter.Object,
            _playlistsPresenter.Object,
            _playStatus.Object,
            _errorHandler.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };

    [Test]
    public async Task SearchAndLoadAsync_SearchesLocallyAndLoadsLocalPlaylist()
    {
        var tracks = new[] { Track("x") };
        _trackInfoProvider.Setup(p => p.SearchTracks("q", 50)).ReturnsAsync(tracks);
        string? title = null;

        await _useCase.SearchAndLoadAsync(LocalMusicSource.Id, "q", SearchEntityKind.Tracks, t => title = t);

        Assert.Multiple(() =>
        {
            _cache.Verify(c => c.ReplaceSearchTracks(LocalMusicSource.Id, It.Is<IEnumerable<Track>>(x => x.SequenceEqual(tracks))), Times.Once);
            _trackInfoProvider.Verify(p => p.SaveAsync(It.IsAny<Track>()), Times.Never);
            _repository.Verify(
                r => r.SetPlaylist(
                    It.Is<Playlist>(p => p.Kind == "search" && p.SourceId == LocalMusicSource.Id),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _playlistsPresenter.Verify(p => p.NotifyTransientPlaylistActive(It.IsAny<Playlist>()), Times.Once);
            Assert.That(title, Does.Contain("Поиск"));
        });
    }

    [Test]
    public async Task SearchAndLoadAsync_WhenNoResults_ReportsAndDoesNotLoadPlaylist()
    {
        _trackInfoProvider.Setup(p => p.SearchTracks("q", 50)).ReturnsAsync(Array.Empty<Track>());

        await _useCase.SearchAndLoadAsync(LocalMusicSource.Id, "q", SearchEntityKind.Tracks, _ => { });

        Assert.Multiple(() =>
        {
            _playStatus.Verify(p => p.SetPlayStatus(It.Is<string>(s => s.Contains("ничего не найдено"))), Times.Once);
            _repository.Verify(r => r.SetPlaylist(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()), Times.Never);
            _playlistsPresenter.Verify(p => p.NotifyTransientPlaylistActive(It.IsAny<Playlist>()), Times.Never);
        });
    }

    [Test]
    public async Task SearchAndLoadAsync_WhenSearchThrows_HandlesError()
    {
        var ex = new InvalidOperationException("boom");
        _trackInfoProvider.Setup(p => p.SearchTracks("q", 50)).ThrowsAsync(ex);

        await _useCase.SearchAndLoadAsync(LocalMusicSource.Id, "q", SearchEntityKind.Tracks, _ => { });

        _errorHandler.Verify(e => e.Handle(ex), Times.Once);
    }

    [Test]
    public async Task LoadSelectionAsync_CachesSelectionWithoutSaving()
    {
        var selected = new List<Track> { Track("a") };
        string? title = null;

        await _useCase.LoadSelectionAsync(YandexMusicSourceId.Value, selected, t => title = t);

        Assert.Multiple(() =>
        {
            _trackInfoProvider.Verify(p => p.SaveAsync(It.IsAny<Track>()), Times.Never);
            _cache.Verify(c => c.ReplaceSearchTracks(YandexMusicSourceId.Value, It.IsAny<IEnumerable<Track>>()), Times.Once);
            _tracksPresenter.Verify(
                t => t.LoadTracksFor(It.Is<Playlist>(p => p.Description == "Результаты поиска")),
                Times.Once);
            Assert.That(title, Does.Contain("Поиск"));
        });
    }

    [Test]
    public async Task LoadLocalSelectionAsync_CachesSelectionWithoutSaving()
    {
        var selected = new List<Track> { Track("a") };

        await _useCase.LoadSelectionAsync(LocalMusicSource.Id, selected, _ => { });

        Assert.Multiple(() =>
        {
            _trackInfoProvider.Verify(p => p.SaveAsync(It.IsAny<Track>()), Times.Never);
            _cache.Verify(c => c.ReplaceSearchTracks(LocalMusicSource.Id, It.IsAny<IEnumerable<Track>>()), Times.Once);
            _repository.Verify(
                r => r.SetPlaylist(It.Is<Playlist>(p => p.Kind == "search"), It.IsAny<CancellationToken>()),
                Times.Once);
        });
    }
}
