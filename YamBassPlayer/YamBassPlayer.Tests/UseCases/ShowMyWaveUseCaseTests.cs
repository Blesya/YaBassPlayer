using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.UseCases;

namespace YamBassPlayer.Tests.UseCases;

[TestFixture]
public sealed class ShowMyWaveUseCaseTests
{
    private Mock<IMyWavePresenter> _myWavePresenter = null!;
    private Mock<IMyWaveWindowPresenter> _myWaveWindowPresenter = null!;
    private Mock<IPlaybackPresenter> _playbackPresenter = null!;
    private Mock<IPlaylistsPresenter> _playlistsPresenter = null!;
    private Mock<IPlayStatusPresenter> _playStatus = null!;
    private Mock<IPlaybackQueue> _playbackQueue = null!;
    private ShowMyWaveUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _myWavePresenter = new Mock<IMyWavePresenter>();
        _myWaveWindowPresenter = new Mock<IMyWaveWindowPresenter>();
        _playbackPresenter = new Mock<IPlaybackPresenter>();
        _playlistsPresenter = new Mock<IPlaylistsPresenter>();
        _playStatus = new Mock<IPlayStatusPresenter>();
        _playbackQueue = new Mock<IPlaybackQueue>();

        _useCase = new ShowMyWaveUseCase(
            _myWavePresenter.Object,
            _myWaveWindowPresenter.Object,
            _playbackPresenter.Object,
            _playlistsPresenter.Object,
            _playStatus.Object,
            _playbackQueue.Object);
    }

    private static Playlist WavePlaylist() => new("Моя волна", PlaylistType.MyWave)
    {
        Description = "Персональная радиостанция Яндекс.Музыки",
        TrackCount = 5
    };

    [Test]
    public async Task ShowAsync_WhenPlaylistReady_ActivatesPlaylist()
    {
        var playlist = WavePlaylist();
        _myWavePresenter.Setup(p => p.StartMyWaveAsync()).ReturnsAsync(playlist);
        string? title = null;

        await _useCase.ShowAsync(t => title = t);

        Assert.Multiple(() =>
        {
            _playbackPresenter.Verify(p => p.SetPlaylistType(PlaylistType.MyWave), Times.Once);
            _playlistsPresenter.Verify(p => p.NotifyTransientPlaylistActive(playlist), Times.Once);
            _myWaveWindowPresenter.Verify(w => w.ShowWindow(playlist), Times.Once);
            Assert.That(title, Is.EqualTo("Моя волна : Персональная радиостанция Яндекс.Музыки"));
        });
    }

    [Test]
    public async Task ShowAsync_WhenPlaylistIsNull_DoesNothing()
    {
        _myWavePresenter.Setup(p => p.StartMyWaveAsync()).ReturnsAsync((Playlist?)null);

        await _useCase.ShowAsync(_ => { });

        Assert.Multiple(() =>
        {
            _playbackPresenter.Verify(p => p.SetPlaylistType(It.IsAny<PlaylistType>()), Times.Never);
            _myWaveWindowPresenter.Verify(w => w.ShowWindow(It.IsAny<Playlist>()), Times.Never);
            _playlistsPresenter.Verify(p => p.NotifyTransientPlaylistActive(It.IsAny<Playlist>()), Times.Never);
        });
    }

    [Test]
    public async Task ShowByTrackAsync_WhenNoCurrentTrack_Reports()
    {
        _playbackQueue.SetupGet(q => q.CurrentTrackId).Returns((string?)null);

        await _useCase.ShowByTrackAsync(_ => { });

        Assert.Multiple(() =>
        {
            _playStatus.Verify(p => p.SetPlayStatus("Сначала начните воспроизведение трека"), Times.Once);
            _myWavePresenter.Verify(p => p.StartMyWaveFromTrackAsync(It.IsAny<string>()), Times.Never);
        });
    }

    [Test]
    public async Task ShowByTrackAsync_StartsWaveFromCurrentTrack()
    {
        var playlist = WavePlaylist();
        _playbackQueue.SetupGet(q => q.CurrentTrackId).Returns("seed");
        _myWavePresenter.Setup(p => p.StartMyWaveFromTrackAsync("seed")).ReturnsAsync(playlist);

        await _useCase.ShowByTrackAsync(_ => { });

        Assert.Multiple(() =>
        {
            _myWavePresenter.Verify(p => p.StartMyWaveFromTrackAsync("seed"), Times.Once);
            _myWaveWindowPresenter.Verify(w => w.ShowWindow(playlist), Times.Once);
        });
    }
}
