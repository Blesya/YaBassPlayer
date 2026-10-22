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
}
