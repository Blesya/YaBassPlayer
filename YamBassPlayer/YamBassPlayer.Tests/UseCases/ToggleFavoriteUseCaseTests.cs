using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.UseCases;

namespace YamBassPlayer.Tests.UseCases;

[TestFixture]
public sealed class ToggleFavoriteUseCaseTests
{
    private Mock<ITrackFavoriteService> _favoriteService = null!;
    private Mock<IPlayStatusPresenter> _playStatus = null!;
    private Mock<ITrackSourceDetector> _sourceDetector = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private ToggleFavoriteUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _favoriteService = new Mock<ITrackFavoriteService>();
        _playStatus = new Mock<IPlayStatusPresenter>();
        _sourceDetector = new Mock<ITrackSourceDetector>();
        _errorHandler = new Mock<IErrorHandler>();

        _favoriteService.Setup(s => s.SupportsSource(It.IsAny<string>())).Returns(true);
        _favoriteService.Setup(s => s.AddToFavorites(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _favoriteService.Setup(s => s.RemoveFromFavorites(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _sourceDetector.Setup(d => d.GetSourceId(It.IsAny<string>())).Returns(SourceIds.Yandex);

        _useCase = new ToggleFavoriteUseCase(
            _favoriteService.Object, _playStatus.Object, _sourceDetector.Object, _errorHandler.Object);
    }

    [Test]
    public async Task ExecuteAsync_WhenNotFavorite_AddsToFavorites()
    {
        _favoriteService.Setup(s => s.IsTrackFavorite(SourceIds.Yandex, "t1")).Returns(false);

        await _useCase.ExecuteAsync(SourceIds.Yandex, "t1");

        Assert.Multiple(() =>
        {
            _favoriteService.Verify(s => s.AddToFavorites(SourceIds.Yandex, "t1"), Times.Once);
            _favoriteService.Verify(s => s.RemoveFromFavorites(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _playStatus.Verify(p => p.SetPlayStatus("Добавлен в избранное"), Times.Once);
            _playStatus.Verify(p => p.SetCurrentTrack("t1", SourceIds.Yandex), Times.Once);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenFavorite_RemovesFromFavorites()
    {
        _favoriteService.Setup(s => s.IsTrackFavorite(SourceIds.Yandex, "t1")).Returns(true);

        await _useCase.ExecuteAsync(SourceIds.Yandex, "t1");

        Assert.Multiple(() =>
        {
            _favoriteService.Verify(s => s.RemoveFromFavorites(SourceIds.Yandex, "t1"), Times.Once);
            _favoriteService.Verify(s => s.AddToFavorites(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _playStatus.Verify(p => p.SetPlayStatus("Удалён из избранного"), Times.Once);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenSourceUnsupported_ReportsAndDoesNotToggle()
    {
        _favoriteService.Setup(s => s.SupportsSource(SourceIds.Yandex)).Returns(false);

        await _useCase.ExecuteAsync(SourceIds.Yandex, "t1");

        Assert.Multiple(() =>
        {
            _playStatus.Verify(p => p.SetPlayStatus("Источник избранного недоступен"), Times.Once);
            _favoriteService.Verify(s => s.AddToFavorites(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _favoriteService.Verify(s => s.RemoveFromFavorites(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenToggleThrows_HandlesErrorAndReports()
    {
        var ex = new InvalidOperationException("boom");
        _favoriteService.Setup(s => s.IsTrackFavorite(SourceIds.Yandex, "t1")).Throws(ex);

        await _useCase.ExecuteAsync(SourceIds.Yandex, "t1");

        Assert.Multiple(() =>
        {
            _errorHandler.Verify(e => e.Handle(ex), Times.Once);
            _playStatus.Verify(p => p.SetPlayStatus("Не удалось обновить избранное"), Times.Once);
        });
    }
}
