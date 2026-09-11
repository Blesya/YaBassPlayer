using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.Tests.Presenters;
using YamBassPlayer.UseCases;

namespace YamBassPlayer.Tests.UseCases;

[TestFixture]
public sealed class ScanLibraryUseCaseTests
{
    private Mock<ILocalLibraryService> _library = null!;
    private Mock<ILocalFolderManagerPresenter> _folderManager = null!;
    private Mock<IPlaylistsPresenter> _playlistsPresenter = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private ScanLibraryUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _library = new Mock<ILocalLibraryService>();
        _folderManager = new Mock<ILocalFolderManagerPresenter>();
        _playlistsPresenter = new Mock<IPlaylistsPresenter>();
        _errorHandler = new Mock<IErrorHandler>();

        _folderManager.Setup(p => p.ShowAsync()).Returns(Task.CompletedTask);

        _useCase = new ScanLibraryUseCase(
            _library.Object,
            _folderManager.Object,
            _playlistsPresenter.Object,
            new FakeUiDispatcher(),
            _errorHandler.Object);
    }

    [Test]
    public async Task AddFolderAsync_AddsFolderAndRefreshesTree()
    {
        _library.Setup(l => l.AddFolderAsync("/music")).ReturnsAsync(new LocalFolder(1, "/music", "music"));

        await _useCase.AddFolderAsync("/music");

        Assert.Multiple(() =>
        {
            _library.Verify(l => l.AddFolderAsync("/music"), Times.Once);
            _playlistsPresenter.Verify(p => p.LoadPlaylistTree(), Times.Once);
        });
    }

    [Test]
    public async Task AddFolderAsync_WhenLibraryThrows_HandlesErrorAndDoesNotRefresh()
    {
        var ex = new InvalidOperationException("boom");
        _library.Setup(l => l.AddFolderAsync("/music")).ThrowsAsync(ex);

        await _useCase.AddFolderAsync("/music");

        Assert.Multiple(() =>
        {
            _errorHandler.Verify(e => e.Handle(ex), Times.Once);
            _playlistsPresenter.Verify(p => p.LoadPlaylistTree(), Times.Never);
        });
    }

    [Test]
    public async Task ScanAllFoldersAsync_ScansRefreshesAndReturnsCount()
    {
        _library.Setup(l => l.ScanAllFoldersAsync(It.IsAny<IProgress<string>>())).ReturnsAsync(5);

        int? count = await _useCase.ScanAllFoldersAsync();

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(5));
            _library.Verify(l => l.ScanAllFoldersAsync(It.IsAny<IProgress<string>>()), Times.Once);
            _playlistsPresenter.Verify(p => p.LoadPlaylistTree(), Times.Once);
        });
    }

    [Test]
    public async Task ScanAllFoldersAsync_WhenScanThrows_HandlesErrorAndReturnsNull()
    {
        var ex = new InvalidOperationException("boom");
        _library.Setup(l => l.ScanAllFoldersAsync(It.IsAny<IProgress<string>>())).ThrowsAsync(ex);

        int? count = await _useCase.ScanAllFoldersAsync();

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.Null);
            _errorHandler.Verify(e => e.Handle(ex), Times.Once);
            _playlistsPresenter.Verify(p => p.LoadPlaylistTree(), Times.Never);
        });
    }

    [Test]
    public async Task ShowFolderManagerAsync_RefreshesTreeAndUnsubscribesAfterClose()
    {
        _folderManager
            .Setup(p => p.ShowAsync())
            .Callback(() => _folderManager.Raise(p => p.OnLibraryChanged += null))
            .Returns(Task.CompletedTask);

        await _useCase.ShowFolderManagerAsync();

        _playlistsPresenter.Verify(p => p.LoadPlaylistTree(), Times.Once);

        // После закрытия подписка снята — повторное событие не должно обновлять дерево.
        _folderManager.Raise(p => p.OnLibraryChanged += null);
        _playlistsPresenter.Verify(p => p.LoadPlaylistTree(), Times.Once);
    }
}
