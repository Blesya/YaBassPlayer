using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Tests.Views;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

/// <summary>
/// Наследует headless-харнесс: обработчики презентера используют
/// <c>Application.MainLoop.Invoke</c>, поэтому нужен инициализированный драйвер и <c>Pump()</c>.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class LocalFolderManagerPresenterTests : ViewTestBase
{
    private Mock<ILocalFolderManagerView> _view = null!;
    private Mock<ILocalLibraryService> _library = null!;
    private LocalFolderManagerPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _view = new Mock<ILocalFolderManagerView>();
        _library = new Mock<ILocalLibraryService>();
        _library.Setup(s => s.GetFoldersAsync()).ReturnsAsync(new List<LocalFolder>());
        _presenter = new LocalFolderManagerPresenter(_view.Object, _library.Object);
    }

    [Test]
    public async Task ShowAsync_LoadsFoldersIntoView()
    {
        await _presenter.ShowAsync();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetFolders(It.IsAny<IReadOnlyList<LocalFolder>>()), Times.Once);
            _view.Verify(v => v.Show(), Times.Once);
        });
    }

    [Test]
    public async Task ShowAsync_WhenScanAllClicked_ScansAndReportsCount()
    {
        _library.Setup(s => s.ScanAllFoldersAsync(It.IsAny<IProgress<string>>())).ReturnsAsync(5);
        _view.Setup(v => v.Show()).Callback(() => _view.Raise(v => v.OnScanAllClicked += null));

        bool changed = false;
        _presenter.OnLibraryChanged += () => changed = true;

        await _presenter.ShowAsync();
        Pump();

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowScanProgress(It.IsAny<string>()), Times.AtLeastOnce);
            _view.Verify(v => v.ShowScanCompleted(5), Times.Once);
            Assert.That(changed, Is.True);
        });
    }

    [Test]
    public async Task ShowAsync_WhenScanFolderClicked_ScansThatFolder()
    {
        _library.Setup(s => s.ScanFolderAsync(7, It.IsAny<IProgress<string>>())).ReturnsAsync(3);
        _view.Setup(v => v.Show()).Callback(() => _view.Raise(v => v.OnScanFolderClicked += null, 7));

        await _presenter.ShowAsync();
        Pump();

        Assert.Multiple(() =>
        {
            _library.Verify(s => s.ScanFolderAsync(7, It.IsAny<IProgress<string>>()), Times.Once);
            _view.Verify(v => v.ShowScanCompleted(3), Times.Once);
        });
    }

    [Test]
    public void ShowAsync_WhenCloseClicked_ClosesView()
    {
        _view.Setup(v => v.Show()).Callback(() => _view.Raise(v => v.OnCloseClicked += null));

        _presenter.ShowAsync().GetAwaiter().GetResult();

        _view.Verify(v => v.Close(), Times.Once);
    }
}
