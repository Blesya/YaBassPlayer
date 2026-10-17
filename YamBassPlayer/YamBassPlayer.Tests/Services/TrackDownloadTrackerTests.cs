using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class TrackDownloadTrackerTests
{
    [Test]
    public void MarkDownloading_RaisesChangedForThatTrack()
    {
        var tracker = new TrackDownloadTracker();
        var events = new List<(string TrackId, TrackDownloadState State)>();
        tracker.Changed += (trackId, state) => events.Add((trackId, state));

        tracker.MarkDownloading("t1");

        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].TrackId, Is.EqualTo("t1"));
            Assert.That(events[0].State.Status, Is.EqualTo(TrackDownloadStatus.Downloading));
        });
    }

    [Test]
    public void ReportProgress_CarriesPercentAndKnownTotal()
    {
        var tracker = new TrackDownloadTracker();
        TrackDownloadState? last = null;
        tracker.Changed += (_, state) => last = state;

        tracker.ReportProgress("t1", new DownloadProgress(450, 1000));

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

        tracker.ReportProgress("t1", new DownloadProgress(2048, 0));

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
        tracker.MarkDownloading("t1");

        tracker.MarkCached("t1");

        Assert.That(tracker.GetActive(), Is.Empty);
    }

    [Test]
    public void GetActive_ReturnsDownloadingAndFailedOnly()
    {
        var tracker = new TrackDownloadTracker();
        tracker.MarkDownloading("t1");
        tracker.MarkFailed("t2");
        tracker.MarkCached("t3");

        var active = tracker.GetActive();

        Assert.That(active.Select(a => a.TrackId), Is.EquivalentTo(new[] { "t1", "t2" }));
    }

    [Test]
    public void GetActive_IsEmptyForFreshTracker()
        => Assert.That(new TrackDownloadTracker().GetActive(), Is.Empty);
}
