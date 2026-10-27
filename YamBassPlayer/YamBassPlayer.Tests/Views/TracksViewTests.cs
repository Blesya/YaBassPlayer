using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class TracksViewTests : ViewTestBase
{
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private const int Width = 40;
    private const int Height = 6;

    private TracksView CreateView(int width = Width) => AddToTop(new TracksView { Width = width, Height = Height });

    private static Track Track(string title, string artist, string id)
        => new(title, artist, "Альбом", id) { SourceId = YandexMusicSourceId.Value };

    // KeyModifiers в Terminal.Gui 1.x не имеет члена None — используем default (0).
    private static KeyEvent Press(Key key) => new(key, default);

    private static IEnumerable<Track> Many(int count)
        => Enumerable.Range(0, count).Select(i => Track($"Трек{i}", $"Исполнитель{i}", $"id{i}"));

    /// <summary>Одна строка вывода по индексу: 0 — заголовок колонок, 1 — первый трек.</summary>
    private string Row(int index, int cols)
        => RenderText(index + 1, cols).Split(Environment.NewLine)[index];

    // ── Рендер ────────────────────────────────────────────────────────────

    [Test]
    public void SetTracks_RendersArtistAndTitle()
    {
        var view = CreateView();

        view.SetTracks([Track("Мой заголовок", "Мой исполнитель", "id1")], _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        string text = RenderText(Height, Width);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Мой исполнитель"));
            Assert.That(text, Does.Contain("Мой заголовок"));
        });
    }

    [Test]
    public void SetTracks_MarksDownloadedTracksWithAsterisk()
    {
        var view = CreateView(width: 60);

        view.SetTracks([Track("T", "A", "id1")], t => t.Id == "id1");
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        Assert.That(Row(1, 60), Does.Contain("*"));
    }

    [Test]
    public void SetTracks_NotDownloaded_HasNoAsterisk()
    {
        var view = CreateView(width: 60);

        view.SetTracks([Track("T", "A", "id1")], _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        Assert.That(Row(1, 60), Does.Not.Contain("*"));
    }

    [Test]
    public void Redraw_DrawsHeaderWithColumnTitles()
    {
        var view = CreateView(width: 60);

        view.SetTracks([Track("T", "A", "id1")], _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        string header = RenderText(1, 60);
        Assert.Multiple(() =>
        {
            Assert.That(header, Does.Contain("Исполнитель"));
            Assert.That(header, Does.Contain("Название"));
        });
    }

    [Test]
    public void Redraw_SelectedRowWithoutFocus_IsNotMarkedAsPlaying()
    {
        var view = CreateView();
        view.SetTracks([Track("T", "A", "id1")], _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        // «▶» помечает играющий трек, а не выбранную строку.
        Assert.That(Row(1, Width), Does.Not.Contain("▶"));
    }

    // ── Прогресс загрузки в списке ────────────────────────────────────────

    [Test]
    public void SetDownloadState_WhileDownloading_ShowsPercent()
    {
        var view = CreateView(width: 60);
        view.SetTracks([Track("T", "A", "id1")], _ => false);
        Pump();

        view.SetDownloadState(K("id1"), new TrackDownloadState(TrackDownloadStatus.Downloading, 45, 450, true));
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        Assert.That(Row(1, 60), Does.Contain("45%"));
    }

    [Test]
    public void SetDownloadState_WhenFinished_ReplacesPercentWithCacheMark()
    {
        var view = CreateView(width: 60);
        view.SetTracks([Track("T", "A", "id1")], _ => false);
        Pump();
        view.SetDownloadState(K("id1"), new TrackDownloadState(TrackDownloadStatus.Downloading, 45, 450, true));
        Pump();

        view.SetDownloadState(K("id1"), TrackDownloadState.Cached);
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        Assert.Multiple(() =>
        {
            Assert.That(Row(1, 60), Does.Contain("*"));
            Assert.That(Row(1, 60), Does.Not.Contain("45%"));
        });
    }

    [Test]
    public void SetDownloadState_Failed_ShowsErrorMark()
    {
        var view = CreateView(width: 60);
        view.SetTracks([Track("T", "A", "id1")], _ => false);
        Pump();

        view.SetDownloadState(K("id1"), TrackDownloadState.Failed);
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        Assert.That(Row(1, 60), Does.Contain("!"));
    }

    [Test]
    public void SetDownloadState_WithoutKnownTotal_ShowsReceivedSize()
    {
        var view = CreateView(width: 60);
        view.SetTracks([Track("T", "A", "id1")], _ => false);
        Pump();

        view.SetDownloadState(K("id1"), new TrackDownloadState(TrackDownloadStatus.Downloading, 0, 2 * 1024 * 1024, false));
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        Assert.That(Row(1, 60), Does.Contain("2M"));
    }

    [Test]
    public void Redraw_MoreTracksThanRows_DrawsScrollIndicator()
    {
        var view = CreateView(width: 60);
        view.SetTracks(Many(50), _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, 60, Height));

        Assert.That(RenderText(Height, 60), Does.Contain("█"));
    }

    [Test]
    public void Redraw_WithDurations_DrawsDurationColumn()
    {
        var view = CreateView(width: 76);
        view.SetTracks([new Track("T", "A", "Альбом", "id1") { SourceId = YandexMusicSourceId.Value, DurationMs = 215_000 }], _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, 76, Height));

        Assert.Multiple(() =>
        {
            Assert.That(Row(0, 76), Does.Contain("Время"));
            Assert.That(Row(1, 76), Does.Contain("3:35"));
        });
    }

    [Test]
    public void Redraw_WideList_SplitsArtistAndTitleIntoColumns()
    {
        var view = CreateView(width: 76);
        view.SetTracks([Track("Мой заголовок", "Мой исполнитель", "id1")], _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, 76, Height));

        string row = Row(1, 76);
        Assert.Multiple(() =>
        {
            Assert.That(row, Does.Contain("Мой исполнитель"));
            Assert.That(row, Does.Contain("Мой заголовок"));
            // Колонки выровнены: между исполнителем и названием есть отбивка.
            Assert.That(row.IndexOf("Мой заголовок", StringComparison.Ordinal),
                Is.GreaterThan(row.IndexOf("Мой исполнитель", StringComparison.Ordinal) + "Мой исполнитель".Length));
        });
    }

    [Test]
    public void SetPlayingTrack_RendersPlayingMarker()
    {
        var view = CreateView();
        view.SetTracks([Track("T1", "A1", "id1")], _ => false);
        Pump();

        view.SetPlayingTrack(K("id1"));
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("▶"));
    }

    // ── Фильтр ────────────────────────────────────────────────────────────

    [Test]
    public void SetFilter_MatchesCaseInsensitivelyAndHidesOthers()
    {
        var view = CreateView();
        view.SetTracks([Track("Песня", "Кино", "id1"), Track("Song", "Queen", "id2")], _ => false);
        Pump();

        view.SetFilter("кино");
        view.Redraw(new Rect(0, 0, Width, Height));

        string text = RenderText(Height, Width);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Кино"));
            Assert.That(text, Does.Not.Contain("Queen"));
        });
    }

    [Test]
    public void SetFilter_NullRestoresAllTracks()
    {
        var view = CreateView();
        view.SetTracks([Track("Песня", "Кино", "id1"), Track("Song", "Queen", "id2")], _ => false);
        Pump();

        view.SetFilter("кино");
        view.SetFilter(null);
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Queen"));
    }

    // ── Навигация ─────────────────────────────────────────────────────────

    [Test]
    public void ProcessKey_CursorDown_RaisesOnTrackSelectedWithNextIndex()
    {
        var view = CreateView();
        view.SetTracks(Many(3), _ => false);
        Pump();

        int? selected = null;
        view.OnTrackSelected += i => selected = i;

        view.ProcessKey(Press(Key.CursorDown));

        Assert.That(selected, Is.EqualTo(1));
    }

    [Test]
    public void ProcessKey_CursorUpAtTop_DoesNotMove()
    {
        var view = CreateView();
        view.SetTracks(Many(3), _ => false);
        Pump();

        int raised = 0;
        view.OnTrackSelected += _ => raised++;

        view.ProcessKey(Press(Key.CursorUp));

        Assert.That(raised, Is.Zero);
    }

    [Test]
    public void ProcessKey_EndSelectsLast_HomeSelectsFirst()
    {
        var view = CreateView();
        view.SetTracks(Many(5), _ => false);
        Pump();

        int? last = null;
        view.OnTrackSelected += i => last = i;
        view.ProcessKey(Press(Key.End));
        Assert.That(last, Is.EqualTo(4));

        int? first = null;
        view.OnTrackSelected += i => first = i;
        view.ProcessKey(Press(Key.Home));
        Assert.That(first, Is.EqualTo(0));
    }

    [Test]
    public void ProcessKey_Enter_RaisesOnCellActivatedWithSelectedIndex()
    {
        var view = CreateView();
        view.SetTracks(Many(3), _ => false);
        Pump();
        view.ProcessKey(Press(Key.CursorDown));

        int? activated = null;
        view.OnCellActivated += i => activated = i;

        view.ProcessKey(Press(Key.Enter));

        Assert.That(activated, Is.EqualTo(1));
    }

    [Test]
    public void ProcessKey_NearEnd_RaisesNeedMoreTracksOnce()
    {
        var view = CreateView();
        view.SetTracks(Many(4), _ => false);
        Pump();

        int calls = 0;
        view.NeedMoreTracks += () => calls++;

        view.ProcessKey(Press(Key.CursorDown));
        view.ProcessKey(Press(Key.CursorDown));

        Assert.That(calls, Is.EqualTo(1));
    }

    // ── Добавление/очистка ────────────────────────────────────────────────

    [Test]
    public void AddTracks_AppendsNewRows()
    {
        var view = CreateView();
        view.SetTracks([Track("Первый", "A1", "id1")], _ => false);
        Pump();

        view.AddTracks([Track("Второй", "A2", "id2")], _ => false);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        string text = RenderText(Height, Width);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Первый"));
            Assert.That(text, Does.Contain("Второй"));
        });
    }

    [Test]
    public void ClearTracks_RemovesAllRows()
    {
        var view = CreateView();
        view.SetTracks([Track("УникальноеИмя", "A1", "id1")], _ => false);
        Pump();

        view.ClearTracks();
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Not.Contain("УникальноеИмя"));
    }

    // ── Пустое состояние ──────────────────────────────────────────────────

    [Test]
    public void EmptyState_WithoutPlaylist_SaysChoosePlaylist()
    {
        var view = CreateView();
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Выберите плейлист"));
    }

    [Test]
    public void EmptyState_WhileLoading_SaysLoading()
    {
        var view = CreateView();
        view.SetState(TrackListState.Loading);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Загрузка плейлиста"));
    }

    [Test]
    public void EmptyState_WhenPlaylistEmpty_SaysEmpty()
    {
        var view = CreateView();
        view.SetState(TrackListState.Ready);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Плейлист пуст"));
    }
}
