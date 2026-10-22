using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.UseCases;

namespace YamBassPlayer.Tests.UseCases;

[TestFixture]
public sealed class ToggleFavoriteUseCaseTests
{
    private static readonly TrackKey Key = new(YandexMusicSourceId.Value, "t1");

    private Mock<ILocalFavoriteService> _favorites = null!;
    private Mock<IPlayStatusPresenter> _playStatus = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private ToggleFavoriteUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _favorites = new Mock<ILocalFavoriteService>();
        _playStatus = new Mock<IPlayStatusPresenter>();
        _errorHandler = new Mock<IErrorHandler>();

        _favorites.Setup(s => s.AddToFavorites(It.IsAny<TrackKey>())).Returns(Task.CompletedTask);
        _favorites.Setup(s => s.RemoveFromFavorites(It.IsAny<TrackKey>())).Returns(Task.CompletedTask);

        _useCase = new ToggleFavoriteUseCase(
            _favorites.Object, _playStatus.Object, _errorHandler.Object);
    }

    [Test]
    public async Task ExecuteAsync_WhenNotFavorite_AddsToFavorites()
    {
        _favorites.Setup(s => s.IsTrackFavorite(Key)).Returns(false);

        await _useCase.ExecuteAsync(Key);

        Assert.Multiple(() =>
        {
            _favorites.Verify(s => s.AddToFavorites(Key), Times.Once);
            _favorites.Verify(s => s.RemoveFromFavorites(It.IsAny<TrackKey>()), Times.Never);
            _playStatus.Verify(p => p.SetPlayStatus("Добавлен в избранное"), Times.Once);
            _playStatus.Verify(p => p.SetCurrentTrack(Key), Times.Once);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenFavorite_RemovesFromFavorites()
    {
        _favorites.Setup(s => s.IsTrackFavorite(Key)).Returns(true);

        await _useCase.ExecuteAsync(Key);

        Assert.Multiple(() =>
        {
            _favorites.Verify(s => s.RemoveFromFavorites(Key), Times.Once);
            _favorites.Verify(s => s.AddToFavorites(It.IsAny<TrackKey>()), Times.Never);
            _playStatus.Verify(p => p.SetPlayStatus("Удалён из избранного"), Times.Once);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenToggleThrows_HandlesErrorAndReports()
    {
        var ex = new InvalidOperationException("boom");
        _favorites.Setup(s => s.IsTrackFavorite(Key)).Throws(ex);

        await _useCase.ExecuteAsync(Key);

        Assert.Multiple(() =>
        {
            _errorHandler.Verify(e => e.Handle(ex), Times.Once);
            _playStatus.Verify(p => p.SetPlayStatus("Не удалось обновить избранное"), Times.Once);
        });
    }
}
