using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackDownloadTrackerTests
{
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    [Test]
    public void MarkDownloading_RaisesChangedForThatTrack()
    {
        var tracker = new TrackDownloadTracker();
        var events = new List<(TrackKey Key, TrackDownloadState State)>();
        tracker.Changed += (key, state) => events.Add((key, state));

        tracker.MarkDownloading(K("t1"));

        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].Key, Is.EqualTo(K("t1")));
            Assert.That(events[0].State.Status, Is.EqualTo(TrackDownloadStatus.Downloading));
        });
    }

    [Test]
    public void ReportProgress_CarriesPercentAndKnownTotal()
    {
        var tracker = new TrackDownloadTracker();
        TrackDownloadState? last = null;
        tracker.Changed += (_, state) => last = state;

        tracker.ReportProgress(K("t1"), new DownloadProgress(450, 1000));

        Assert.Multiple(() =>
        {
            Assert.That(last!.Value.Status, Is.EqualTo(TrackDownloadStatus.Downloading));
            Assert.That(last!.Value.Percent, Is.EqualTo(45));
            Assert.That(last!.Value.HasTotal, Is.True);
        });
    }

    [Test]
    public void ReportProgress_WithoutTotal_KeepsReceivedBytes()
    {
        var tracker = new TrackDownloadTracker();
        TrackDownloadState? last = null;
        tracker.Changed += (_, state) => last = state;

        tracker.ReportProgress(K("t1"), new DownloadProgress(2048, 0));

        Assert.Multiple(() =>
        {
            Assert.That(last!.Value.HasTotal, Is.False);
            Assert.That(last!.Value.BytesReceived, Is.EqualTo(2048));
        });
    }

    [Test]
    public void MarkCached_DropsTrackFromActive()
    {
        var tracker = new TrackDownloadTracker();
        tracker.MarkDownloading(K("t1"));

        tracker.MarkCached(K("t1"));

        Assert.That(tracker.GetActive(), Is.Empty);
    }

    [Test]
    public void GetActive_ReturnsDownloadingAndFailedOnly()
    {
        var tracker = new TrackDownloadTracker();
        tracker.MarkDownloading(K("t1"));
        tracker.MarkFailed(K("t2"));
        tracker.MarkCached(K("t3"));

        var active = tracker.GetActive();

        Assert.That(active.Select(a => a.Key), Is.EquivalentTo(new[] { K("t1"), K("t2") }));
    }

    [Test]
    public void GetActive_IsEmptyForFreshTracker()
        => Assert.That(new TrackDownloadTracker().GetActive(), Is.Empty);
}
