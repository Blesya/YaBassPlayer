using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class YandexSearchPresenterTests
{
    private Mock<ISourceSearchService> _searchService = null!;
    private Mock<IViewFactory> _viewFactory = null!;
    private Mock<IYandexSearchView> _view = null!;
    private YandexSearchPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _searchService = new Mock<ISourceSearchService>();
        _viewFactory = new Mock<IViewFactory>();
        _view = new Mock<IYandexSearchView>();
        _viewFactory.Setup(f => f.Create<IYandexSearchView>()).Returns(_view.Object);
        _presenter = new YandexSearchPresenter(_searchService.Object, _viewFactory.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);
    private static Artist Artist(string id) => new(id, $"Artist-{id}");
    private static Album Album(string id) => new(id, $"Album-{id}");

    // ── Открытие диалога ──────────────────────────────────────────────────

    [Test]
    public void ShowYandexSearchDialog_ShowsCreatedView()
    {
        _presenter.ShowYandexSearchDialog();

        Assert.Multiple(() =>
        {
            _viewFactory.Verify(f => f.Create<IYandexSearchView>(), Times.Once);
            _view.Verify(v => v.Show(), Times.Once);
        });
    }

    // ── Поиск ─────────────────────────────────────────────────────────────

    [Test]
    public void SearchClicked_WithBlankQuery_ShowsErrorWithoutCallingService()
    {
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnSearchClicked += null, "  ");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowError(It.Is<string>(s => s.Contains("Введите текст"))), Times.Once);
            _searchService.Verify(s => s.SearchAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        });
    }

    [Test]
    public void SearchClicked_PerformsSearchAndTogglesLoading()
    {
        var result = new SearchResult { Tracks = [Track("a")] };
        _searchService.Setup(s => s.SearchAllAsync(SourceIds.Yandex, "кино", 20)).ReturnsAsync(result);
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnSearchClicked += null, "кино");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetLoading(true), Times.Once);
            _view.Verify(v => v.SetSearchResults(result), Times.Once);
            _view.Verify(v => v.SetLoading(false), Times.Once);
        });
    }

    [Test]
    public void SearchFailure_ShowsErrorAndStopsLoading()
    {
        _searchService.Setup(s => s.SearchAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnSearchClicked += null, "кино");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowError(It.Is<string>(s => s.Contains("boom"))), Times.Once);
            _view.Verify(v => v.SetLoading(false), Times.Once);
        });
    }

    // ── OK: разворачивание выбранных элементов ────────────────────────────

    [Test]
    public void OkClicked_WithNoMarkedItems_ShowsErrorAndDoesNotClose()
    {
        _view.Setup(v => v.GetMarkedItems()).Returns([]);
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowError(It.IsAny<string>()), Times.Once);
            _view.Verify(v => v.Close(), Times.Never);
        });
    }

    [Test]
    public void OkClicked_WithTrackItem_ReturnsTrack()
    {
        var track = Track("t1");
        _view.Setup(v => v.GetMarkedItems()).Returns([new TrackItem(track)]);
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(new[] { track }));
            Assert.That(_presenter.WasCancelled(), Is.False);
            _view.Verify(v => v.Close(), Times.Once);
        });
    }

    [Test]
    public void OkClicked_WithArtistItem_ExpandsArtistTracks()
    {
        var artistTracks = new[] { Track("a1"), Track("a2") };
        _view.Setup(v => v.GetMarkedItems()).Returns([new ArtistItem(Artist("ar"))]);
        _searchService.Setup(s => s.GetArtistTracksAsync(SourceIds.Yandex, "ar")).ReturnsAsync(artistTracks);
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(artistTracks));
    }

    [Test]
    public void OkClicked_WithAlbumItem_ExpandsAlbumTracks()
    {
        var albumTracks = new[] { Track("al1") };
        _view.Setup(v => v.GetMarkedItems()).Returns([new AlbumItem(Album("al"))]);
        _searchService.Setup(s => s.GetAlbumTracksAsync(SourceIds.Yandex, "al")).ReturnsAsync(albumTracks);
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(albumTracks));
    }

    [Test]
    public void CancelClicked_SetsCancelledAndCloses()
    {
        _presenter.ShowYandexSearchDialog();

        _view.Raise(v => v.OnCancelClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(_presenter.WasCancelled(), Is.True);
            _view.Verify(v => v.Close(), Times.Once);
        });
    }
}
