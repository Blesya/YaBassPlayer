using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class PlaylistRowsViewTests : ViewTestBase
{
    private const int Width = 60;
    private const int Height = 6;   // 2 карточки по 3 строки

    private PlaylistRowsView CreateView() => AddToTop(new PlaylistRowsView { Width = Width, Height = Height });

    private static Track Track(string title, string artist, string id) => new(title, artist, "Диск", id);

    private static KeyEvent Press(Key key) => new(key, default);

    // ── Рендер ────────────────────────────────────────────────────────────

    [Test]
    public void SetTracks_RendersCardContent()
    {
        var view = CreateView();

        view.SetTracks([Track("Песня", "Кино", "id1")]);
        view.Redraw(new Rect(0, 0, Width, Height));

        string text = RenderText(Height, Width);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Кино"));
            Assert.That(text, Does.Contain("Песня"));
        });
    }

    [Test]
    public void Redraw_WithNoTracks_ShowsPlaceholder()
    {
        var view = CreateView();

        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Плейлист не загружен"));
    }

    [Test]
    public void SetCurrentTrackId_RendersPlayingMarker()
    {
        var view = CreateView();
        view.SetTracks([Track("Первая", "A1", "id1"), Track("Вторая", "A2", "id2")]);

        view.SetCurrentTrackId("id2");
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("▶"));
    }

    // ── Навигация и активация ─────────────────────────────────────────────

    [Test]
    public void ProcessKey_Enter_ActivatesSelectedTrack()
    {
        var view = CreateView();
        view.SetTracks([Track("Первая", "A1", "id1"), Track("Вторая", "A2", "id2")]);

        string? activated = null;
        view.OnTrackActivated += id => activated = id;

        view.ProcessKey(Press(Key.Enter));

        Assert.That(activated, Is.EqualTo("id1"));
    }

    [Test]
    public void ProcessKey_CursorDownThenEnter_ActivatesSecondTrack()
    {
        var view = CreateView();
        view.SetTracks([Track("Первая", "A1", "id1"), Track("Вторая", "A2", "id2")]);

        string? activated = null;
        view.OnTrackActivated += id => activated = id;

        view.ProcessKey(Press(Key.CursorDown));
        view.ProcessKey(Press(Key.Enter));

        Assert.That(activated, Is.EqualTo("id2"));
    }

    [Test]
    public void MouseDoubleClick_ActivatesClickedRow()
    {
        var view = CreateView();
        view.SetTracks([Track("Первая", "A1", "id1"), Track("Вторая", "A2", "id2")]);

        string? activated = null;
        view.OnTrackActivated += id => activated = id;

        // Высота карточки 3 строки: Y = 3 → вторая карточка.
        view.MouseEvent(new MouseEvent { X = 1, Y = 3, Flags = MouseFlags.Button1DoubleClicked });

        Assert.That(activated, Is.EqualTo("id2"));
    }
}
