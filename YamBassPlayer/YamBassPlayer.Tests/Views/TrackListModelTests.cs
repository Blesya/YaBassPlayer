using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
public sealed class TrackListModelTests
{
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private static Track Track(string id) => new($"Title-{id}", $"Artist-{id}", "Album", id) { SourceId = YandexMusicSourceId.Value };

    private static TrackListModel CreateModel(params string[] ids)
    {
        var model = new TrackListModel();
        model.SetTracks(ids.Select(Track), _ => false);
        return model;
    }

    [Test]
    public void SelectByTrack_SelectsMatchingIndex()
    {
        var model = CreateModel("a", "b", "c");

        bool found = model.SelectByTrack(K("b"));

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(model.SelectedIndex, Is.EqualTo(1));
        });
    }

    [Test]
    public void SelectByTrack_UnknownId_KeepsSelection()
    {
        var model = CreateModel("a", "b");
        model.Select(1);

        bool found = model.SelectByTrack(K("zzz"));

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(model.SelectedIndex, Is.EqualTo(1));
        });
    }

    [Test]
    public void SelectByTrack_RespectsFilter()
    {
        var model = CreateModel("a", "b");
        model.SetFilter("Title-a");

        Assert.That(model.SelectByTrack(K("b")), Is.False);
    }

    [Test]
    public void SelectByTrack_FiresChangedOnce()
    {
        var model = CreateModel("a", "b");
        int changes = 0;
        model.Changed += () => changes++;

        model.SelectByTrack(K("b"));

        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void SetFilter_NarrowsItems()
    {
        var model = CreateModel("a", "b", "c");

        model.SetFilter("Title-b");

        Assert.Multiple(() =>
        {
            Assert.That(model.Items, Has.Count.EqualTo(1));
            Assert.That(model.Items[0].Track.Title, Is.EqualTo("Title-b"));
            Assert.That(model.SelectedIndex, Is.EqualTo(0));
        });
    }

    [Test]
    public void SetDownloadState_WithoutFilter_UpdatesVisibleItems()
    {
        var model = CreateModel("a", "b");

        model.SetDownloadState(K("b"), TrackDownloadState.Cached);

        Assert.Multiple(() =>
        {
            Assert.That(model.Items[0].Download, Is.EqualTo(TrackDownloadState.Idle));
            Assert.That(model.Items[1].Download, Is.EqualTo(TrackDownloadState.Cached));
        });
    }

    [Test]
    public void SetDownloadState_WithFilter_KeepsFilterApplied()
    {
        var model = CreateModel("a", "b");
        model.SetFilter("Title-b");

        model.SetDownloadState(K("a"), TrackDownloadState.Cached);

        Assert.Multiple(() =>
        {
            Assert.That(model.Items, Has.Count.EqualTo(1), "фильтр не должен сбрасываться");
            Assert.That(model.Items[0].Track.Title, Is.EqualTo("Title-b"));
            Assert.That(model.Items[0].Download, Is.EqualTo(TrackDownloadState.Idle),
                "состояние трека вне фильтра видимый список не меняет");
        });
    }
}
