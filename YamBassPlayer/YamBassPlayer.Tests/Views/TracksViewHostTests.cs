using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class TracksViewHostTests : ViewTestBase
{
    private const int Width = 55;   // у плиток: 55 / (26 + 1) = 2 колонки
    private const int Height = 5;

    private (TracksViewHost Host, TracksTileView Tiles, TracksView Table) CreateHost()
    {
        var model = new TrackListModel();
        var tiles = new TracksTileView(model) { Width = Width, Height = Height };
        var table = new TracksView(model) { Width = Width, Height = Height };
        var host = new TracksViewHost(tiles, table, model) { Width = Width, Height = Height };
        AddToTop(host);
        return (host, tiles, table);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static Track Track(string title, string artist, string id)
        => new(title, artist, "Альбом", id);

    private static KeyEvent Press(Key key) => new(key, default);

    private static IEnumerable<Track> Many(int count)
        => Enumerable.Range(0, count).Select(i => Track($"T{i}", $"A{i}", $"id{i}"));

    // ── Переключение режима ───────────────────────────────────────────────

    [Test]
    public void SetTracksViewMode_False_ShowsTableAndHidesTiles()
    {
        var (host, tiles, table) = CreateHost();

        host.SetTracksViewMode(false);

        Assert.Multiple(() =>
        {
            Assert.That(host.IsTilesActive, Is.False);
            Assert.That(tiles.Visible, Is.False);
            Assert.That(table.Visible, Is.True);
        });
    }

    [Test]
    public void SetTracksViewMode_True_ShowsTilesAndHidesTable()
    {
        var (host, tiles, table) = CreateHost();
        host.SetTracksViewMode(false);

        host.SetTracksViewMode(true);

        Assert.Multiple(() =>
        {
            Assert.That(host.IsTilesActive, Is.True);
            Assert.That(tiles.Visible, Is.True);
            Assert.That(table.Visible, Is.False);
        });
    }

    // ── Рассылка состояния обеим реализациям ──────────────────────────────

    [Test]
    public void SetTracks_ForwardsToTiles()
    {
        var (host, tiles, _) = CreateHost();
        host.SetTracks([Track("Песня", "АртистПлитки", "id1")], _ => false);
        Pump();
        host.SetFilter(null);

        tiles.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("АртистПлитки"));
    }

    [Test]
    public void SetTracks_ForwardsToTable()
    {
        var (host, _, table) = CreateHost();
        host.SetTracks([Track("Песня", "АртистТаблицы", "id1")], _ => false);
        Pump();

        table.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("АртистТаблицы"));
    }

    [Test]
    public void AddTracks_AppendsToBothViews()
    {
        var (host, tiles, table) = CreateHost();
        host.SetTracks([Track("Первый", "A1", "id1")], _ => false);
        Pump();
        host.AddTracks([Track("Второй", "A2", "id2")], _ => false);
        Pump();
        host.SetFilter(null);

        table.Redraw(new Rect(0, 0, Width, Height));
        Assert.That(RenderText(Height, Width), Does.Contain("Второй"));

        tiles.Redraw(new Rect(0, 0, Width, Height));
        Assert.That(RenderText(Height, Width), Does.Contain("A2"));
    }

    // ── Регрессия: общая модель не должна получать AddTracks дважды ────────

    [Test]
    public void AddTracks_AppendsOnce_SharedModelDoesNotDuplicate()
    {
        // Ширина на 3 плитки, чтобы возможный дубликат был виден в рендере.
        const int width = 82;
        const int height = 5;

        var model = new TrackListModel();
        var tiles = new TracksTileView(model) { Width = width, Height = height };
        var table = new TracksView(model) { Width = width, Height = height };
        var host = new TracksViewHost(tiles, table, model) { Width = width, Height = height };
        AddToTop(host);

        host.SetTracks([Track("ПервыйТрек", "АртистПервый", "id1")], _ => false);
        Pump();
        host.AddTracks([Track("ВторойТрек", "АртистВторой", "id2")], _ => false);
        Pump();
        host.SetFilter(null);

        table.Redraw(new Rect(0, 0, width, height));
        Assert.That(CountOccurrences(RenderText(height, width), "АртистВторой"), Is.EqualTo(1));

        tiles.Redraw(new Rect(0, 0, width, height));
        Assert.That(CountOccurrences(RenderText(height, width), "АртистВторой"), Is.EqualTo(1));
    }

    [Test]
    public void ClearTracks_ClearsBothViews()
    {
        var (host, tiles, table) = CreateHost();
        host.SetTracks([Track("УникальноеИмя", "A1", "id1")], _ => false);
        Pump();

        host.ClearTracks();
        Pump();
        host.SetFilter(null);

        table.Redraw(new Rect(0, 0, Width, Height));
        Assert.That(RenderText(Height, Width), Does.Not.Contain("УникальноеИмя"));
    }

    [Test]
    public void SetPlayingTrackId_ForwardsToBothViews()
    {
        var (host, tiles, table) = CreateHost();
        host.SetTracks([Track("T1", "A1", "id1")], _ => false);
        Pump();
        host.SetFilter(null);

        host.SetPlayingTrackId("id1");
        Pump();

        table.Redraw(new Rect(0, 0, Width, Height));
        Assert.That(RenderText(Height, Width), Does.Contain("▶"));

        tiles.Redraw(new Rect(0, 0, Width, Height));
        Assert.That(RenderText(Height, Width), Does.Contain("▶"));
    }

    // ── Проброс событий ───────────────────────────────────────────────────

    [Test]
    public void TilesEvent_Selected_IsForwardedToHost()
    {
        var (host, tiles, _) = CreateHost();
        host.SetTracksViewMode(true);
        host.SetTracks(Many(4), _ => false);
        Pump();
        host.SetFilter(null);
        host.Redraw(new Rect(0, 0, Width, Height)); // вычисляет колонки плиток

        int? selected = null;
        host.OnTrackSelected += i => selected = i;

        tiles.ProcessKey(Press(Key.CursorDown));

        Assert.That(selected, Is.EqualTo(2));
    }

    [Test]
    public void TilesEvent_Activated_IsForwardedToHost()
    {
        var (host, tiles, _) = CreateHost();
        host.SetTracksViewMode(true);
        host.SetTracks(Many(2), _ => false);
        Pump();
        host.SetFilter(null);
        host.Redraw(new Rect(0, 0, Width, Height));

        int? activated = null;
        host.OnCellActivated += i => activated = i;

        tiles.ProcessKey(Press(Key.Enter));

        Assert.That(activated, Is.EqualTo(0));
    }

    [Test]
    public void TableEvent_Selected_IsForwardedToHost()
    {
        var (host, _, table) = CreateHost();
        host.SetTracksViewMode(false);
        host.SetTracks(Many(3), _ => false);
        Pump();

        int? selected = null;
        host.OnTrackSelected += i => selected = i;

        table.ProcessKey(Press(Key.CursorDown));

        Assert.That(selected, Is.EqualTo(1));
    }

    [Test]
    public void TableEvent_NeedMoreTracks_IsForwardedToHost()
    {
        var (host, _, table) = CreateHost();
        host.SetTracksViewMode(false);
        host.SetTracks(Many(4), _ => false);
        Pump();

        int calls = 0;
        host.NeedMoreTracks += () => calls++;

        table.ProcessKey(Press(Key.CursorDown));
        table.ProcessKey(Press(Key.CursorDown));

        Assert.That(calls, Is.EqualTo(1));
    }
}
