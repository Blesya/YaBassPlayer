using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
public sealed class TrackListModelTests
{
    private static Track Track(string id) => new($"Title-{id}", $"Artist-{id}", "Album", id);

    private static TrackListModel CreateModel(params string[] ids)
    {
        var model = new TrackListModel();
        model.SetTracks(ids.Select(Track), _ => false);
        return model;
    }

    [Test]
    public void SelectByTrackId_SelectsMatchingIndex()
    {
        var model = CreateModel("a", "b", "c");

        bool found = model.SelectByTrackId("b");

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(model.SelectedIndex, Is.EqualTo(1));
        });
    }

    [Test]
    public void SelectByTrackId_UnknownId_KeepsSelection()
    {
        var model = CreateModel("a", "b");
        model.Select(1);

        bool found = model.SelectByTrackId("zzz");

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(model.SelectedIndex, Is.EqualTo(1));
        });
    }

    [Test]
    public void SelectByTrackId_RespectsFilter()
    {
        var model = CreateModel("a", "b");
        model.SetFilter("Title-a");

        Assert.That(model.SelectByTrackId("b"), Is.False);
    }

    [Test]
    public void SelectByTrackId_FiresChangedOnce()
    {
        var model = CreateModel("a", "b");
        int changes = 0;
        model.Changed += () => changes++;

        model.SelectByTrackId("b");

        Assert.That(changes, Is.EqualTo(1));
    }
}
