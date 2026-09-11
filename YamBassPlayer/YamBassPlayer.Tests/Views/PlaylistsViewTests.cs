using Terminal.Gui;
using Terminal.Gui.Trees;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class PlaylistsViewTests : ViewTestBase
{
    private const int Width = 60;
    private const int Height = 10;

    private PlaylistsView CreateView() => AddToTop(new PlaylistsView { Width = Width, Height = Height });

    private static Playlist Pl(string name, PlaylistType type = PlaylistType.Favorite, int count = 3)
        => new(name, type) { TrackCount = count };

    private static TreeView? FindTree(View root)
    {
        if (root is TreeView tree)
            return tree;

        foreach (var child in root.Subviews)
        {
            var found = FindTree(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    // ── Построение дерева ─────────────────────────────────────────────────

    [Test]
    public void SetPlaylistTree_RendersPlaylistNameAndCount()
    {
        var view = CreateView();

        view.SetPlaylistTree([PlaylistTreeItem.FromPlaylist(Pl("Избранное", PlaylistType.Favorite, 7))]);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Избранное (7)"));
    }

    // ── Отметка «играет» ──────────────────────────────────────────────────

    [Test]
    public void MarkAsPlaying_AddsPlayingPrefix()
    {
        var view = CreateView();
        var playlist = Pl("Избранное");
        view.SetPlaylistTree([PlaylistTreeItem.FromPlaylist(playlist)]);
        Pump();

        view.MarkAsPlaying(playlist);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("▶"));
    }

    [Test]
    public void MarkAsPlaying_WithNull_RemovesPlayingPrefix()
    {
        var view = CreateView();
        var playlist = Pl("Избранное");
        view.SetPlaylistTree([PlaylistTreeItem.FromPlaylist(playlist)]);
        Pump();
        view.MarkAsPlaying(playlist);
        Pump();

        view.MarkAsPlaying(null);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Not.Contain("▶"));
    }

    // ── Временные плейлисты ───────────────────────────────────────────────

    [Test]
    public void AddOrUpdateTransientPlaylist_AddsNewRoot()
    {
        var view = CreateView();
        view.SetPlaylistTree([PlaylistTreeItem.FromPlaylist(Pl("Избранное", PlaylistType.Favorite))]);
        Pump();

        view.AddOrUpdateTransientPlaylist(Pl("Результаты поиска", PlaylistType.YandexSearch));
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Результаты поиска"));
    }

    [Test]
    public void AddOrUpdateTransientPlaylist_ReplacesRootOfSameType()
    {
        var view = CreateView();
        view.SetPlaylistTree([PlaylistTreeItem.FromPlaylist(Pl("Старый поиск", PlaylistType.YandexSearch))]);
        Pump();

        view.AddOrUpdateTransientPlaylist(Pl("Новый поиск", PlaylistType.YandexSearch));
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        string text = RenderText(Height, Width);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Новый поиск"));
            Assert.That(text, Does.Not.Contain("Старый поиск"));
        });
    }

    [Test]
    public void AddOrUpdateTransientPlaylist_UnderParentTag_AddsChild()
    {
        var view = CreateView();
        var parent = new PlaylistTreeItem
        {
            Label = "Яндекс",
            Tag = "ya",
            IsExpandedByDefault = true
        };
        view.SetPlaylistTree([parent]);
        Pump();

        var transient = new Playlist("Треки исполнителя", PlaylistType.YandexSearch) { ParentTag = "ya" };
        view.AddOrUpdateTransientPlaylist(transient);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Assert.That(RenderText(Height, Width), Does.Contain("Треки исполнителя"));
    }

    // ── Выбор плейлиста ───────────────────────────────────────────────────

    [Test]
    public void PlaylistSelected_IsRaisedWhenSelectionChanges()
    {
        var view = CreateView();
        var first = PlaylistTreeItem.FromPlaylist(Pl("Первый", PlaylistType.Favorite));
        var second = PlaylistTreeItem.FromPlaylist(Pl("Второй", PlaylistType.Custom));
        view.SetPlaylistTree([first, second]);
        Pump();
        view.Redraw(new Rect(0, 0, Width, Height));

        Playlist? selected = null;
        view.PlaylistSelected += p => selected = p;

        var tree = FindTree(view);
        Assert.That(tree, Is.Not.Null);
        tree!.SetFocus();
        tree.ProcessKey(new KeyEvent(Key.CursorDown, default));

        Assert.That(selected, Is.Not.Null);
    }
}
