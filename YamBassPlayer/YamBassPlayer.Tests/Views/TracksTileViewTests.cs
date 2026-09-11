using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class TracksTileViewTests : ViewTestBase
{
    private const int Width = 55;   // 2 колонки: 55 / (26 + 1) = 2
    private const int Height = 5;   // 1 ряд плиток: 5 / 5 = 1

    private TracksTileView CreateView() => AddToTop(new TracksTileView { Width = Width, Height = Height });

    private static Track Track(string title, string artist, string id)
        => new(title, artist, "Альбом", id);

    private static KeyEvent Press(Key key) => new(key, default);

    private static IEnumerable<Track> Many(int count)
        => Enumerable.Range(0, count).Select(i => Track($"T{i}", $"A{i}", $"id{i}"));

    // Плитки раскрываются анимацией по тику таймера; SetFilter(null) сразу выставляет
    // _revealedCount = _tracks.Count, что делает рендер детерминированным в тестах.
    private static void RevealAll(TracksTileView view) => view.SetFilter(null);

    [Test]
    public void SetTracks_RendersTileContent()
    {
        var view = CreateView();
        view.SetTracks([Track("Заголовок", "Исполнитель", "id1")], _ => false);
        Pump();
        RevealAll(view);

        view.Redraw(new Rect(0, 0, Width, Height));

        string text = RenderText(Height, Width);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Исполнитель"));
            Assert.That(text, Does.Contain("Заголовок"));
        });
    }

    [Test]
    public void Redraw_DrawsTwoTilesPerRowAtThisWidth()
    {
        var view = CreateView();
        view.SetTracks([Track("A", "ArtistA", "id1"), Track("B", "ArtistB", "id2")], _ => false);
        Pump();
        RevealAll(view);

        view.Redraw(new Rect(0, 0, Width, Height));

        // Строка 0 — верхние рамки плиток, строка 1 — строка исполнителя.
        string[] lines = RenderText(Height, Width).Split(Environment.NewLine);
        Assert.Multiple(() =>
        {
            Assert.That(lines[1], Does.Contain("ArtistA"));
            Assert.That(lines[1], Does.Contain("ArtistB"));
        });
    }

    [Test]
    public void ProcessKey_CursorDown_MovesByColumnCount()
    {
        var view = CreateView();
        view.SetTracks(Many(4), _ => false);
        Pump();
        RevealAll(view);
        view.Redraw(new Rect(0, 0, Width, Height)); // вычисляет _columns

        int? selected = null;
        view.OnTrackSelected += i => selected = i;

        view.ProcessKey(Press(Key.CursorDown));

        Assert.That(selected, Is.EqualTo(2));
    }

    [Test]
    public void ProcessKey_Enter_RaisesOnCellActivated()
    {
        var view = CreateView();
        view.SetTracks(Many(2), _ => false);
        Pump();
        RevealAll(view);
        view.Redraw(new Rect(0, 0, Width, Height));

        int? activated = null;
        view.OnCellActivated += i => activated = i;

        view.ProcessKey(Press(Key.Enter));

        Assert.That(activated, Is.EqualTo(0));
    }

    [Test]
    public void MouseClickOnSecondTile_SelectsIt()
    {
        var view = CreateView();
        view.SetTracks(Many(4), _ => false);
        Pump();
        RevealAll(view);
        view.Redraw(new Rect(0, 0, Width, Height));

        int? selected = null;
        view.OnTrackSelected += i => selected = i;

        var click = new MouseEvent { X = 27, Y = 0, Flags = MouseFlags.Button1Clicked };
        view.MouseEvent(click);

        Assert.That(selected, Is.EqualTo(1));
    }

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
    public void SetPlayingTrackId_RendersMarker()
    {
        var view = CreateView();
        view.SetTracks([Track("T1", "A1", "id1")], _ => false);
        Pump();
        RevealAll(view);

        view.SetPlayingTrackId("id1");
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("▶"));
    }
}
