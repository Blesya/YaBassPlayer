using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class LocalSearchPresenterTests
{
    private Mock<ITrackInfoProvider> _trackInfoProvider = null!;
    private Mock<IViewFactory> _viewFactory = null!;
    private Mock<ILocalSearchView> _view = null!;
    private LocalSearchPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _trackInfoProvider = new Mock<ITrackInfoProvider>();
        _viewFactory = new Mock<IViewFactory>();
        _view = new Mock<ILocalSearchView>();
        _viewFactory.Setup(f => f.Create<ILocalSearchView>()).Returns(_view.Object);
        _presenter = new LocalSearchPresenter(_trackInfoProvider.Object, _viewFactory.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);

    private void SetupSearch(params Track[] results)
        => _trackInfoProvider.Setup(p => p.SearchTracks(It.IsAny<string>(), 50)).ReturnsAsync(results);

    // ── Открытие диалога ──────────────────────────────────────────────────

    [Test]
    public void ShowLocalSearchDialog_ShowsCreatedView()
    {
        _presenter.ShowLocalSearchDialog();

        Assert.Multiple(() =>
        {
            _viewFactory.Verify(f => f.Create<ILocalSearchView>(), Times.Once);
            _view.Verify(v => v.Show(), Times.Once);
        });
    }

    // ── Поиск ─────────────────────────────────────────────────────────────

    [Test]
    public void SearchQueryChanged_PushesResultsToView()
    {
        var results = new[] { Track("a"), Track("b") };
        SetupSearch(results);
        _presenter.ShowLocalSearchDialog();

        _view.Raise(v => v.OnSearchQueryChanged += null, "кино");

        _view.Verify(v => v.SetSearchResults(It.Is<IEnumerable<Track>>(t => t.SequenceEqual(results))), Times.Once);
    }

    [Test]
    public void SearchQueryChanged_WithBlankQuery_PushesEmptyResultsAndSkipsProvider()
    {
        _presenter.ShowLocalSearchDialog();

        _view.Raise(v => v.OnSearchQueryChanged += null, "   ");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetSearchResults(It.Is<IEnumerable<Track>>(t => !t.Any())), Times.Once);
            _trackInfoProvider.Verify(p => p.SearchTracks(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        });
    }

    [Test]
    public void SearchFailure_ShowsErrorInView()
    {
        _trackInfoProvider.Setup(p => p.SearchTracks(It.IsAny<string>(), 50))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _presenter.ShowLocalSearchDialog();

        _view.Raise(v => v.OnSearchQueryChanged += null, "кино");

        _view.Verify(v => v.ShowError(It.Is<string>(s => s.Contains("boom"))), Times.Once);
    }

    // ── OK / Cancel ───────────────────────────────────────────────────────

    [Test]
    public void OkClicked_WithNoResults_ShowsErrorAndDoesNotClose()
    {
        _presenter.ShowLocalSearchDialog();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowError(It.IsAny<string>()), Times.Once);
            _view.Verify(v => v.Close(), Times.Never);
        });
    }

    [Test]
    public void OkClicked_WithMarkedTracks_ReturnsMarkedAndCloses()
    {
        var results = new[] { Track("a"), Track("b") };
        var marked = new[] { Track("b") };
        SetupSearch(results);
        _view.Setup(v => v.GetMarkedTracks()).Returns(marked);
        _presenter.ShowLocalSearchDialog();
        _view.Raise(v => v.OnSearchQueryChanged += null, "q");

        _view.Raise(v => v.OnOkClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(marked));
            Assert.That(_presenter.WasCancelled(), Is.False);
            _view.Verify(v => v.Close(), Times.Once);
        });
    }

    [Test]
    public void OkClicked_WithoutMarkedTracks_ReturnsAllResults()
    {
        var results = new[] { Track("a"), Track("b") };
        SetupSearch(results);
        _view.Setup(v => v.GetMarkedTracks()).Returns([]);
        _presenter.ShowLocalSearchDialog();
        _view.Raise(v => v.OnSearchQueryChanged += null, "q");

        _view.Raise(v => v.OnOkClicked += null);

        Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(results));
    }

    [Test]
    public void CancelClicked_SetsCancelledAndCloses()
    {
        _presenter.ShowLocalSearchDialog();

        _view.Raise(v => v.OnCancelClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(_presenter.WasCancelled(), Is.True);
            _view.Verify(v => v.Close(), Times.Once);
        });
    }
}
