using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class YandexSearchPresenterTests
{
    private Mock<IEntitySearchSource> _searchService = null!;
    private Mock<IViewFactory> _viewFactory = null!;
    private Mock<IYandexSearchView> _view = null!;
    private YandexSearchPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _searchService = new Mock<IEntitySearchSource>();
        
        _view = new Mock<IYandexSearchView>();
        
        _presenter = new YandexSearchPresenter(_searchService.Object, () => _view.Object, new FakeUiDispatcher());
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };
    private static Artist Artist(string id) => new(id, $"Artist-{id}");
    private static Album Album(string id) => new(id, $"Album-{id}");

    // ── Открытие диалога ──────────────────────────────────────────────────

    [Test]
    public async Task ShowYandexSearchDialog_ShowsCreatedView()
    {
        await _presenter.ShowYandexSearchDialogAsync();

        Assert.Multiple(() =>
        {
                        _view.Verify(v => v.Show(), Times.Once);
        });
    }

    /// <summary>
    /// Пункт меню плагина выполняется в фоновой задаче, а <c>Application.Run</c> из
    /// фонового потока гоняет драйвер Terminal.Gui одновременно с основным циклом
    /// (падение IndexOutOfRangeException в отрисовке). Значит и создание окна,
    /// и его показ обязаны идти через диспетчер UI-потока.
    /// </summary>
    [Test]
    public async Task ShowYandexSearchDialog_CreatesAndShowsViewOnUiDispatcher()
    {
        var dispatcher = new RecordingUiDispatcher();
        bool createdInDispatcher = false;
        bool shownInDispatcher = false;

        _view.Setup(v => v.Show()).Callback(() => shownInDispatcher = dispatcher.IsInvoking);
        var presenter = new YandexSearchPresenter(
            _searchService.Object,
            () =>
            {
                createdInDispatcher = dispatcher.IsInvoking;
                return _view.Object;
            },
            dispatcher);

        await presenter.ShowYandexSearchDialogAsync();

        Assert.Multiple(() =>
        {
            Assert.That(dispatcher.InvokeCount, Is.EqualTo(1));
            Assert.That(createdInDispatcher, Is.True, "окно должно создаваться в UI-потоке");
            Assert.That(shownInDispatcher, Is.True, "Application.Run должен запускаться в UI-потоке");
        });
    }

    /// <summary>Диспетчер, запоминающий, выполняется ли действие прямо сейчас.</summary>
    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public bool IsInvoking { get; private set; }

        public void Invoke(Action action)
        {
            InvokeCount++;
            IsInvoking = true;

            try
            {
                action();
            }
            finally
            {
                IsInvoking = false;
            }
        }

        public void StartTimer(TimeSpan interval, Func<bool> onTick)
        {
        }
    }

    // ── Поиск ─────────────────────────────────────────────────────────────

    [Test]
    public async Task SearchClicked_WithBlankQuery_ShowsErrorWithoutCallingService()
    {
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnSearchClicked += null, "  ");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowError(It.Is<string>(s => s.Contains("Введите текст"))), Times.Once);
            _searchService.Verify(s => s.SearchAllAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task SearchClicked_PerformsSearchAndTogglesLoading()
    {
        var result = new SearchResult { Tracks = [Track("a")] };
        _searchService.Setup(s => s.SearchAllAsync("кино", 20, It.IsAny<CancellationToken>())).ReturnsAsync(result);
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnSearchClicked += null, "кино");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetLoading(true), Times.Once);
            _view.Verify(v => v.SetSearchResults(result), Times.Once);
            _view.Verify(v => v.SetLoading(false), Times.Once);
        });
    }

    [Test]
    public async Task SearchFailure_ShowsErrorAndStopsLoading()
    {
        _searchService.Setup(s => s.SearchAllAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnSearchClicked += null, "кино");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowError(It.Is<string>(s => s.Contains("boom"))), Times.Once);
            _view.Verify(v => v.SetLoading(false), Times.Once);
        });
    }

    // ── OK: разворачивание выбранных элементов ────────────────────────────

    [Test]
    public async Task OkClicked_WithNoMarkedItems_ShowsErrorAndDoesNotClose()
    {
        _view.Setup(v => v.GetMarkedItems()).Returns([]);
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.ShowError(It.IsAny<string>()), Times.Once);
            _view.Verify(v => v.Close(), Times.Never);
        });
    }

    [Test]
    public async Task OkClicked_WithTrackItem_ReturnsTrack()
    {
        var track = Track("t1");
        _view.Setup(v => v.GetMarkedItems()).Returns([new TrackItem(track)]);
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(new[] { track }));
            Assert.That(_presenter.WasCancelled(), Is.False);
            _view.Verify(v => v.Close(), Times.Once);
        });
    }

    [Test]
    public async Task OkClicked_WithArtistItem_ExpandsArtistTracks()
    {
        var artistTracks = new[] { Track("a1"), Track("a2") };
        _view.Setup(v => v.GetMarkedItems()).Returns([new ArtistItem(Artist("ar"))]);
        _searchService.Setup(s => s.GetArtistTracksAsync("ar", It.IsAny<CancellationToken>())).ReturnsAsync(artistTracks);
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(artistTracks));
    }

    [Test]
    public async Task OkClicked_WithAlbumItem_ExpandsAlbumTracks()
    {
        var albumTracks = new[] { Track("al1") };
        _view.Setup(v => v.GetMarkedItems()).Returns([new AlbumItem(Album("al"))]);
        _searchService.Setup(s => s.GetAlbumTracksAsync("al", It.IsAny<CancellationToken>())).ReturnsAsync(albumTracks);
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnOkClicked += null);

        Assert.That(_presenter.GetSelectedTracks(), Is.EqualTo(albumTracks));
    }

    [Test]
    public async Task CancelClicked_SetsCancelledAndCloses()
    {
        await _presenter.ShowYandexSearchDialogAsync();

        _view.Raise(v => v.OnCancelClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(_presenter.WasCancelled(), Is.True);
            _view.Verify(v => v.Close(), Times.Once);
        });
    }
}
