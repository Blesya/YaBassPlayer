namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Enums;
using Moq;

[TestFixture]
public sealed class PlaybackQueueTests
{
    private Mock<IAudioPlayer> _mockAudioPlayer = null!;
    private PlaybackQueue _queue = null!;

    [SetUp]
    public void SetUp()
    {
        _mockAudioPlayer = new Mock<IAudioPlayer>();
        _mockAudioPlayer.SetupAdd(m => m.OnTrackEnded += It.IsAny<EventHandler>());
        _queue = new PlaybackQueue(_mockAudioPlayer.Object);
    }

    // ──────────────── Constructor ────────────────

    [Test]
    public void Constructor_SubscribesToOnTrackEnded()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        var before = _queue.CurrentTrackId;
        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);
        Assert.That(_queue.CurrentTrackId, Is.Not.EqualTo(before));
    }

    // ──────────────── CurrentTrackId ────────────────

    [Test]
    public void CurrentTrackId_ReturnsNull_ForEmptyQueue()
    {
        Assert.That(_queue.CurrentTrackId, Is.Null);
    }

    [Test]
    public void CurrentTrackId_ReturnsCorrectId_AfterSetQueue()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    [Test]
    public void CurrentTrackId_ChangesAfterNext()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("b"));
    }

    [Test]
    public void CurrentTrackId_ChangesAfterPrevious()
    {
        _queue.SetQueue(new[] { "a", "b", "c" }, startIndex: 1);
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    // ──────────────── HasNext / HasPrevious ────────────────

    [Test]
    public void HasNext_Sequential_TrueWhenNotAtEnd()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        Assert.That(_queue.HasNext, Is.True);
    }

    [Test]
    public void HasNext_Sequential_FalseAtEnd()
    {
        _queue.SetQueue(new[] { "a", "b" }, startIndex: 1);
        Assert.That(_queue.HasNext, Is.False);
    }

    [Test]
    public void HasPrevious_Sequential_TrueWhenNotAtStart()
    {
        _queue.SetQueue(new[] { "a", "b", "c" }, startIndex: 1);
        Assert.That(_queue.HasPrevious, Is.True);
    }

    [Test]
    public void HasPrevious_Sequential_FalseAtStart()
    {
        _queue.SetQueue(new[] { "a", "b" }, startIndex: 0);
        Assert.That(_queue.HasPrevious, Is.False);
    }

    [Test]
    public void HasNext_Sequential_AfterWrapFromEnd_IsTrue()
    {
        _queue.SetQueue(new[] { "a", "b", "c" }, startIndex: 2);
        _queue.Next();
        Assert.That(_queue.HasNext, Is.True);
    }

    [Test]
    public void HasNext_Shuffle_TrueWhenItemsExist()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        Assert.That(_queue.HasNext, Is.True);
    }

    [Test]
    public void HasNext_Shuffle_FalseWhenQueueEmpty()
    {
        _queue.Mode = PlaybackMode.Shuffle;
        Assert.That(_queue.HasNext, Is.False);
    }

    [Test]
    public void HasPrevious_Shuffle_TrueWhenHistoryNotEmpty()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.HasPrevious, Is.True);
    }

    [Test]
    public void HasPrevious_Shuffle_FalseWhenHistoryEmpty()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        Assert.That(_queue.HasPrevious, Is.False);
    }

    // ──────────────── PeekNextTrackId ────────────────

    [Test]
    public void PeekNextTrackId_Sequential_ReturnsNextTrack()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        Assert.That(_queue.PeekNextTrackId, Is.EqualTo("b"));
    }

    [Test]
    public void PeekNextTrackId_Sequential_NullAtEnd()
    {
        _queue.SetQueue(new[] { "a" }, startIndex: 0);
        Assert.That(_queue.PeekNextTrackId, Is.Null);
    }

    [Test]
    public void PeekNextTrackId_Shuffle_ReturnsSomeTrack()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        Assert.That(_queue.PeekNextTrackId, Is.Not.Null);
    }

    [Test]
    public void PeekNextTrackId_Shuffle_DeterministicOnRepeatedCalls()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        _queue.Mode = PlaybackMode.Shuffle;
        var first = _queue.PeekNextTrackId;
        var second = _queue.PeekNextTrackId;
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void PeekNextTrackId_EmptyQueue_ReturnsNull()
    {
        Assert.That(_queue.PeekNextTrackId, Is.Null);
    }

    // ──────────────── SetQueue ────────────────

    [Test]
    public void SetQueue_SetsCurrentTrackId_ToFirstTrack()
    {
        _queue.SetQueue(new[] { "x", "y", "z" });
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("x"));
    }

    [Test]
    public void SetQueue_WithStartIndex_SetsCurrentTrackId()
    {
        _queue.SetQueue(new[] { "x", "y", "z" }, startIndex: 2);
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("z"));
    }

    [Test]
    public void SetQueue_FiresOnTrackChanged()
    {
        string? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.SetQueue(new[] { "track1", "track2" });
        Assert.That(changed, Is.EqualTo("track1"));
    }

    [Test]
    public void SetQueue_DoesNotFireOnTrackChanged_WhenStartIndexOutOfBounds()
    {
        string? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.SetQueue(new[] { "track1", "track2" }, startIndex: -1);
        Assert.That(changed, Is.Null);
    }

    [Test]
    public void SetQueue_WithEmptyList_ClearsState()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.SetQueue(Array.Empty<string>());
        Assert.Multiple(() =>
        {
            Assert.That(_queue.CurrentTrackId, Is.Null);
            Assert.That(_queue.HasNext, Is.False);
            Assert.That(_queue.HasPrevious, Is.False);
            Assert.That(_queue.PeekNextTrackId, Is.Null);
            Assert.That(_queue.TrackIds, Is.Empty);
        });
    }

    [Test]
    public void SetQueue_ResetsShuffleHistory()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.HasPrevious, Is.True, "History should have an entry after Next in Shuffle");

        _queue.SetQueue(new[] { "c", "d" });
        Assert.That(_queue.HasPrevious, Is.False, "History should be cleared after SetQueue");
    }

    [Test]
    public void SetQueue_ReplacesTrackIds()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.SetQueue(new[] { "c", "d", "e" });
        Assert.That(_queue.TrackIds, Is.EqualTo(new[] { "c", "d", "e" }));
    }

    [Test]
    public void SetQueue_ResetsCurrentIndex()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        _queue.Next();
        _queue.Next();
        _queue.SetQueue(new[] { "x", "y" });
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("x"));
    }

    // ──────────────── Next() ────────────────

    [Test]
    public void Next_Sequential_AdvancesIndex()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("b"));
    }

    [Test]
    public void Next_Sequential_WrapsToZeroAtEnd()
    {
        _queue.SetQueue(new[] { "a", "b" }, startIndex: 1);
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    [Test]
    public void Next_Sequential_FiresOnTrackChanged()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        string? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.Next();
        Assert.That(changed, Is.EqualTo("b"));
    }

    [Test]
    public void Next_Shuffle_PicksDifferentTrack()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        var before = _queue.CurrentTrackId;
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.Not.EqualTo(before));
    }

    [Test]
    public void Next_Shuffle_SavesCurrentToHistory()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    [Test]
    public void Next_EmptyQueue_DoesNothing()
    {
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.Null);
    }

    [Test]
    public void Next_Sequential_MultipleTracksAdvancesCorrectly()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        _queue.Next();
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("c"));
    }

    [Test]
    public void Next_Shuffle_WithSingleTrack_StaysOnSameTrack()
    {
        _queue.SetQueue(new[] { "a" });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    // ──────────────── Previous() ────────────────

    [Test]
    public void Previous_Sequential_GoesBackByOne()
    {
        _queue.SetQueue(new[] { "a", "b", "c" }, startIndex: 1);
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    [Test]
    public void Previous_Sequential_WrapsToLastAtStart()
    {
        _queue.SetQueue(new[] { "a", "b" }, startIndex: 0);
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("b"));
    }

    [Test]
    public void Previous_Sequential_FiresOnTrackChanged()
    {
        _queue.SetQueue(new[] { "a", "b" }, startIndex: 1);
        string? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.Previous();
        Assert.That(changed, Is.EqualTo("a"));
    }

    [Test]
    public void Previous_Shuffle_PopsFromHistory()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.Not.EqualTo("a"));
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
        Assert.That(_queue.HasPrevious, Is.False);
    }

    [Test]
    public void Previous_Shuffle_NoHistory_WrapsToLast()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("b"));
    }

    [Test]
    public void Previous_EmptyQueue_DoesNothing()
    {
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.Null);
    }

    [Test]
    public void Previous_Sequential_MultipleTracksGoesBackCorrectly()
    {
        _queue.SetQueue(new[] { "a", "b", "c" }, startIndex: 2);
        _queue.Previous();
        _queue.Previous();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    // ──────────────── Mode ────────────────

    [Test]
    public void Mode_DefaultIsSequential()
    {
        Assert.That(_queue.Mode, Is.EqualTo(PlaybackMode.Sequential));
    }

    [Test]
    public void Mode_CanSetToShuffle()
    {
        _queue.Mode = PlaybackMode.Shuffle;
        Assert.That(_queue.Mode, Is.EqualTo(PlaybackMode.Shuffle));
    }

    [Test]
    public void Mode_CanSwitchBetweenSequentialAndShuffle()
    {
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Mode = PlaybackMode.Sequential;
        Assert.That(_queue.Mode, Is.EqualTo(PlaybackMode.Sequential));
    }

    // ──────────────── AddToQueue ────────────────

    [Test]
    public void AddToQueue_AddsTracksToExistingQueue()
    {
        _queue.SetQueue(new[] { "a", "b" });
        _queue.AddToQueue(new[] { "c", "d" });
        Assert.That(_queue.TrackIds, Is.EqualTo(new[] { "a", "b", "c", "d" }));
    }

    [Test]
    public void AddToQueue_DoesNotAffectCurrentPosition()
    {
        _queue.SetQueue(new[] { "a", "b", "c" }, startIndex: 1);
        var before = _queue.CurrentTrackId;
        _queue.AddToQueue(new[] { "d", "e" });
        Assert.That(_queue.CurrentTrackId, Is.EqualTo(before));
    }

    [Test]
    public void AddToQueue_ToEmptyQueue_AddsTracks()
    {
        _queue.AddToQueue(new[] { "x", "y" });
        Assert.That(_queue.TrackIds, Is.EqualTo(new[] { "x", "y" }));
    }

    // ──────────────── Clear ────────────────

    [Test]
    public void Clear_ResetsAllState()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        _queue.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(_queue.CurrentTrackId, Is.Null);
            Assert.That(_queue.TrackIds, Is.Empty);
            Assert.That(_queue.HasNext, Is.False);
            Assert.That(_queue.HasPrevious, Is.False);
            Assert.That(_queue.PeekNextTrackId, Is.Null);
        });
    }

    // ──────────────── OnTrackEnded ────────────────

    [Test]
    public void OnTrackEnded_CallsNext()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));

        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("b"));
    }

    [Test]
    public void OnTrackEnded_WhenAtEnd_WrapsToStart()
    {
        _queue.SetQueue(new[] { "a", "b" }, startIndex: 1);
        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);
        Assert.That(_queue.CurrentTrackId, Is.EqualTo("a"));
    }

    // ──────────────── TrackIds ────────────────

    [Test]
    public void TrackIds_ReturnsCopy_NotReference()
    {
        _queue.SetQueue(new[] { "a", "b" });
        var ids = _queue.TrackIds;
        _queue.AddToQueue(new[] { "c" });
        Assert.That(ids.Count, Is.EqualTo(2), "Returned list should be a snapshot, not a live reference");
    }

    // ──────────────── PeekNextTrackId matches Next() ────────────────

    [Test]
    public void PeekNextTrackId_Match_AfterNext_Sequential()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        var peeked = _queue.PeekNextTrackId;
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo(peeked));
    }

    [Test]
    public void PeekNextTrackId_Match_AfterNext_Shuffle()
    {
        _queue.SetQueue(new[] { "a", "b", "c" });
        _queue.Mode = PlaybackMode.Shuffle;
        var peeked = _queue.PeekNextTrackId;
        _queue.Next();
        Assert.That(_queue.CurrentTrackId, Is.EqualTo(peeked));
    }
}
