using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class TracksViewTests : ViewTestBase
{
    private const int Width = 40;
    private const int Height = 6;

    private TracksView CreateView() => AddToTop(new TracksView { Width = Width, Height = Height });

    private static Track Track(string title, string artist, string id)
        => new(title, artist, "Альбом", id);

    // KeyModifiers в Terminal.Gui 1.x не имеет члена None — используем default (0).
    private static KeyEvent Press(Key key) => new(key, default);

    private static IEnumerable<Track> Many(int count)
        => Enumerable.Range(0, count).Select(i => Track($"Трек{i}", $"Исполнитель{i}", $"id{i}"));

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
        var view = CreateView();

        view.SetTracks([Track("T", "A", "id1")], id => id == "id1");
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("*"));
    }

    [Test]
    public void SetPlayingTrackId_RendersPlayingMarker()
    {
        var view = CreateView();
        view.SetTracks([Track("T1", "A1", "id1")], _ => false);
        Pump();

        view.SetPlayingTrackId("id1");
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
}
