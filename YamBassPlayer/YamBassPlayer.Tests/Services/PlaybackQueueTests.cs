namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Models;
using YamBassPlayer.Enums;
using Moq;

[TestFixture]
public sealed class PlaybackQueueTests
{
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

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
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        var before = _queue.CurrentTrack;
        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);
        Assert.That(_queue.CurrentTrack, Is.Not.EqualTo(before));
    }

    // ──────────────── LastChangeWasAutomatic ────────────────

    [Test]
    public void LastChangeWasAutomatic_FalseAfterSetQueueAndManualNext()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        Assert.That(_queue.LastChangeWasAutomatic, Is.False);

        _queue.Next();
        Assert.That(_queue.LastChangeWasAutomatic, Is.False);
    }

    [Test]
    public void LastChangeWasAutomatic_TrueAfterExplicitAutomaticAdvance()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });

        _queue.NextAutomatic();

        Assert.That(_queue.LastChangeWasAutomatic, Is.True);
    }

    [Test]
    public void LastChangeWasAutomatic_TrueAfterTrackEnded()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });

        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);

        Assert.That(_queue.LastChangeWasAutomatic, Is.True);
    }

    [Test]
    public void LastChangeWasAutomatic_TrueAfterRepeatRestart()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Mode = PlaybackMode.Repeat;

        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
            Assert.That(_queue.LastChangeWasAutomatic, Is.True);
        });
    }

    // ──────────────── CurrentTrack ────────────────

    [Test]
    public void CurrentTrack_ReturnsNull_ForEmptyQueue()
    {
        Assert.That(_queue.CurrentTrack, Is.Null);
    }

    [Test]
    public void CurrentTrack_ReturnsCorrectId_AfterSetQueue()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    [Test]
    public void CurrentTrack_ChangesAfterNext()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("b")));
    }

    [Test]
    public void CurrentTrack_ChangesAfterPrevious()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") }, startIndex: 1);
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    // ──────────────── HasNext / HasPrevious ────────────────

    [Test]
    public void HasNext_Sequential_TrueWhenNotAtEnd()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        Assert.That(_queue.HasNext, Is.True);
    }

    [Test]
    public void HasNext_Sequential_FalseAtEnd()
    {
        _queue.SetQueue(new[] { K("a"), K("b") }, startIndex: 1);
        Assert.That(_queue.HasNext, Is.False);
    }

    [Test]
    public void HasPrevious_Sequential_TrueWhenNotAtStart()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") }, startIndex: 1);
        Assert.That(_queue.HasPrevious, Is.True);
    }

    [Test]
    public void HasPrevious_Sequential_FalseAtStart()
    {
        _queue.SetQueue(new[] { K("a"), K("b") }, startIndex: 0);
        Assert.That(_queue.HasPrevious, Is.False);
    }

    [Test]
    public void HasNext_Sequential_AfterWrapFromEnd_IsTrue()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") }, startIndex: 2);
        _queue.Next();
        Assert.That(_queue.HasNext, Is.True);
    }

    [Test]
    public void HasNext_Shuffle_TrueWhenItemsExist()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
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
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.HasPrevious, Is.True);
    }

    [Test]
    public void HasPrevious_Shuffle_FalseWhenHistoryEmpty()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        Assert.That(_queue.HasPrevious, Is.False);
    }

    // ──────────────── PeekNext ────────────────

    [Test]
    public void PeekNext_Sequential_ReturnsNextTrack()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        Assert.That(_queue.PeekNext, Is.EqualTo(K("b")));
    }

    [Test]
    public void PeekNext_Sequential_NullAtEnd()
    {
        _queue.SetQueue(new[] { K("a") }, startIndex: 0);
        Assert.That(_queue.PeekNext, Is.Null);
    }

    [Test]
    public void PeekNext_Shuffle_ReturnsSomeTrack()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        Assert.That(_queue.PeekNext, Is.Not.Null);
    }

    [Test]
    public void PeekNext_Shuffle_DeterministicOnRepeatedCalls()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Mode = PlaybackMode.Shuffle;
        var first = _queue.PeekNext;
        var second = _queue.PeekNext;
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void PeekNext_EmptyQueue_ReturnsNull()
    {
        Assert.That(_queue.PeekNext, Is.Null);
    }

    [Test]
    public void PeekNext_Repeat_ReturnsNull()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Repeat;

        Assert.That(_queue.PeekNext, Is.Null, "В повторе следующий трек не нужен и не предзагружается");
    }

    // ──────────────── SetQueue ────────────────

    [Test]
    public void SetQueue_SetsCurrentTrack_ToFirstTrack()
    {
        _queue.SetQueue(new[] { K("x"), K("y"), K("z") });
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("x")));
    }

    [Test]
    public void SetQueue_WithStartIndex_SetsCurrentTrack()
    {
        _queue.SetQueue(new[] { K("x"), K("y"), K("z") }, startIndex: 2);
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("z")));
    }

    [Test]
    public void SetQueue_FiresOnTrackChanged()
    {
        TrackKey? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.SetQueue(new[] { K("track1"), K("track2") });
        Assert.That(changed, Is.EqualTo(K("track1")));
    }

    [Test]
    public void SetQueue_DoesNotFireOnTrackChanged_WhenStartIndexOutOfBounds()
    {
        TrackKey? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.SetQueue(new[] { K("track1"), K("track2") }, startIndex: -1);
        Assert.That(changed, Is.Null);
    }

    [Test]
    public void SetQueue_WithEmptyList_ClearsState()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.SetQueue(Array.Empty<TrackKey>());
        Assert.Multiple(() =>
        {
            Assert.That(_queue.CurrentTrack, Is.Null);
            Assert.That(_queue.HasNext, Is.False);
            Assert.That(_queue.HasPrevious, Is.False);
            Assert.That(_queue.PeekNext, Is.Null);
            Assert.That(_queue.TrackKeys, Is.Empty);
        });
    }

    [Test]
    public void SetQueue_ResetsShuffleHistory()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.HasPrevious, Is.True, "History should have an entry after Next in Shuffle");

        _queue.SetQueue(new[] { K("c"), K("d") });
        Assert.That(_queue.HasPrevious, Is.False, "History should be cleared after SetQueue");
    }

    [Test]
    public void SetQueue_ReplacesTrackKeys()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.SetQueue(new[] { K("c"), K("d"), K("e") });
        Assert.That(_queue.TrackKeys, Is.EqualTo(new[] { K("c"), K("d"), K("e") }));
    }

    [Test]
    public void SetQueue_ResetsCurrentIndex()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Next();
        _queue.Next();
        _queue.SetQueue(new[] { K("x"), K("y") });
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("x")));
    }

    // ──────────────── Next() ────────────────

    [Test]
    public void Next_Sequential_AdvancesIndex()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("b")));
    }

    [Test]
    public void Next_Sequential_WrapsToZeroAtEnd()
    {
        _queue.SetQueue(new[] { K("a"), K("b") }, startIndex: 1);
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    [Test]
    public void Next_Sequential_FiresOnTrackChanged()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        TrackKey? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.Next();
        Assert.That(changed, Is.EqualTo(K("b")));
    }

    [Test]
    public void Next_Shuffle_PicksDifferentTrack()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        var before = _queue.CurrentTrack;
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.Not.EqualTo(before));
    }

    [Test]
    public void Next_Shuffle_SavesCurrentToHistory()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    [Test]
    public void Next_EmptyQueue_DoesNothing()
    {
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.Null);
    }

    [Test]
    public void Next_Sequential_MultipleTracksAdvancesCorrectly()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Next();
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("c")));
    }

    [Test]
    public void Next_Shuffle_WithSingleTrack_StaysOnSameTrack()
    {
        _queue.SetQueue(new[] { K("a") });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    // ──────────────── Previous() ────────────────

    [Test]
    public void Previous_Sequential_GoesBackByOne()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") }, startIndex: 1);
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    [Test]
    public void Previous_Sequential_WrapsToLastAtStart()
    {
        _queue.SetQueue(new[] { K("a"), K("b") }, startIndex: 0);
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("b")));
    }

    [Test]
    public void Previous_Sequential_FiresOnTrackChanged()
    {
        _queue.SetQueue(new[] { K("a"), K("b") }, startIndex: 1);
        TrackKey? changed = null;
        _queue.OnTrackChanged += id => changed = id;
        _queue.Previous();
        Assert.That(changed, Is.EqualTo(K("a")));
    }

    [Test]
    public void Previous_Shuffle_PopsFromHistory()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.Not.EqualTo(K("a")));
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
        Assert.That(_queue.HasPrevious, Is.False);
    }

    [Test]
    public void Previous_Shuffle_NoHistory_WrapsToLast()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("b")));
    }

    [Test]
    public void Previous_EmptyQueue_DoesNothing()
    {
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.Null);
    }

    [Test]
    public void Previous_Sequential_MultipleTracksGoesBackCorrectly()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") }, startIndex: 2);
        _queue.Previous();
        _queue.Previous();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
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

    [Test]
    public void Mode_CanSetToRepeat()
    {
        _queue.Mode = PlaybackMode.Repeat;
        Assert.That(_queue.Mode, Is.EqualTo(PlaybackMode.Repeat));
    }

    // ──────────────── AddToQueue ────────────────

    [Test]
    public void AddToQueue_AddsTracksToExistingQueue()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.AddToQueue(new[] { K("c"), K("d") });
        Assert.That(_queue.TrackKeys, Is.EqualTo(new[] { K("a"), K("b"), K("c"), K("d") }));
    }

    [Test]
    public void AddToQueue_DoesNotAffectCurrentPosition()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") }, startIndex: 1);
        var before = _queue.CurrentTrack;
        _queue.AddToQueue(new[] { K("d"), K("e") });
        Assert.That(_queue.CurrentTrack, Is.EqualTo(before));
    }

    [Test]
    public void AddToQueue_ToEmptyQueue_AddsTracks()
    {
        _queue.AddToQueue(new[] { K("x"), K("y") });
        Assert.That(_queue.TrackKeys, Is.EqualTo(new[] { K("x"), K("y") }));
    }

    // ──────────────── Clear ────────────────

    [Test]
    public void Clear_ResetsAllState()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Mode = PlaybackMode.Shuffle;
        _queue.Next();
        _queue.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(_queue.CurrentTrack, Is.Null);
            Assert.That(_queue.TrackKeys, Is.Empty);
            Assert.That(_queue.HasNext, Is.False);
            Assert.That(_queue.HasPrevious, Is.False);
            Assert.That(_queue.PeekNext, Is.Null);
        });
    }

    // ──────────────── OnTrackEnded ────────────────

    [Test]
    public void OnTrackEnded_CallsNext()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));

        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("b")));
    }

    [Test]
    public void OnTrackEnded_WhenAtEnd_WrapsToStart()
    {
        _queue.SetQueue(new[] { K("a"), K("b") }, startIndex: 1);
        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);
        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    [Test]
    public void OnTrackEnded_Repeat_StaysOnSameTrack()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Repeat;
        TrackKey? changed = null;
        _queue.OnTrackChanged += id => changed = id;

        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
            Assert.That(changed, Is.EqualTo(K("a")), "Тот же трек должен стартовать заново");
        });
    }

    [Test]
    public void OnTrackEnded_Repeat_WithSingleTrack_StaysOnIt()
    {
        _queue.SetQueue(new[] { K("a") });
        _queue.Mode = PlaybackMode.Repeat;

        _mockAudioPlayer.Raise(m => m.OnTrackEnded += null, EventArgs.Empty);

        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("a")));
    }

    [Test]
    public void Next_Repeat_StillAdvancesManually()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        _queue.Mode = PlaybackMode.Repeat;

        _queue.Next();

        Assert.That(_queue.CurrentTrack, Is.EqualTo(K("b")), "Кнопка «Вперёд» двигает очередь и в повторе");
    }

    // ──────────────── TrackKeys ────────────────

    [Test]
    public void TrackKeys_ReturnsCopy_NotReference()
    {
        _queue.SetQueue(new[] { K("a"), K("b") });
        var ids = _queue.TrackKeys;
        _queue.AddToQueue(new[] { K("c") });
        Assert.That(ids.Count, Is.EqualTo(2), "Returned list should be a snapshot, not a live reference");
    }

    // ──────────────── PeekNext matches Next() ────────────────

    [Test]
    public void PeekNext_Match_AfterNext_Sequential()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        var peeked = _queue.PeekNext;
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(peeked));
    }

    [Test]
    public void PeekNext_Match_AfterNext_Shuffle()
    {
        _queue.SetQueue(new[] { K("a"), K("b"), K("c") });
        _queue.Mode = PlaybackMode.Shuffle;
        var peeked = _queue.PeekNext;
        _queue.Next();
        Assert.That(_queue.CurrentTrack, Is.EqualTo(peeked));
    }
}
