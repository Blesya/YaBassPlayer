namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class TrackRepositoryCacheTests
{
    private static Track MakeTrack(string id) => new("Title", "Artist", "Album", id);

    // ──────────────── Initial state ────────────────

    [Test]
    public void InitialState_AllCollectionsAreEmpty()
    {
        var cache = new TrackRepositoryCache();

        Assert.Multiple(() =>
        {
            Assert.That(cache.FavoriteTrackIds, Is.Empty);
            Assert.That(cache.LocalSearchTrackIds, Is.Empty);
            Assert.That(cache.YandexSearchTrackIds, Is.Empty);
            Assert.That(cache.QueueTrackIds, Is.Empty);
            Assert.That(cache.MyWaveTracks, Is.Empty);
        });
    }

    // ──────────────── ReplaceFavoriteTrackIds ────────────────

    [Test]
    public void ReplaceFavoriteTrackIds_ReplacesContent()
    {
        var cache = new TrackRepositoryCache();
        cache.ReplaceFavoriteTrackIds(new[] { "a", "b", "c" });

        Assert.That(cache.FavoriteTrackIds, Is.EqualTo(new[] { "a", "b", "c" }));

        cache.ReplaceFavoriteTrackIds(new[] { "x", "y" });
        Assert.That(cache.FavoriteTrackIds, Is.EqualTo(new[] { "x", "y" }));
    }

    // ──────────────── InsertFavoriteTrackId ────────────────

    [Test]
    public void InsertFavoriteTrackId_InsertsAtBeginning()
    {
        var cache = new TrackRepositoryCache();
        cache.ReplaceFavoriteTrackIds(new[] { "b", "c" });
        cache.InsertFavoriteTrackId("a");

        Assert.That(cache.FavoriteTrackIds, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void InsertFavoriteTrackId_DoesNotInsertDuplicate()
    {
        var cache = new TrackRepositoryCache();
        cache.ReplaceFavoriteTrackIds(new[] { "a", "b" });
        cache.InsertFavoriteTrackId("a");

        Assert.That(cache.FavoriteTrackIds, Is.EqualTo(new[] { "a", "b" }));
    }

    // ──────────────── RemoveFavoriteTrackId ────────────────

    [Test]
    public void RemoveFavoriteTrackId_RemovesExistingItem()
    {
        var cache = new TrackRepositoryCache();
        cache.ReplaceFavoriteTrackIds(new[] { "a", "b", "c" });
        cache.RemoveFavoriteTrackId("b");

        Assert.That(cache.FavoriteTrackIds, Is.EqualTo(new[] { "a", "c" }));
    }

    [Test]
    public void RemoveFavoriteTrackId_NoOp_ForMissingItem()
    {
        var cache = new TrackRepositoryCache();
        cache.ReplaceFavoriteTrackIds(new[] { "a", "b" });

        Assert.DoesNotThrow(() => cache.RemoveFavoriteTrackId("nonexistent"));
        Assert.That(cache.FavoriteTrackIds, Is.EqualTo(new[] { "a", "b" }));
    }

    // ──────────────── Custom playlist cache ────────────────

    [Test]
    public void TryGetCustomPlaylistIds_ReturnsFalse_Initially()
    {
        var cache = new TrackRepositoryCache();

        var found = cache.TryGetCustomPlaylistIds("my-list", out var ids);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(ids, Is.Null);
        });
    }

    [Test]
    public void SetAndTryGetCustomPlaylistIds_ReturnsTrue_AfterSetting()
    {
        var cache = new TrackRepositoryCache();
        var trackIds = new List<string> { "t1", "t2", "t3" };

        cache.SetCustomPlaylistIds("my-list", trackIds);
        var found = cache.TryGetCustomPlaylistIds("my-list", out var retrieved);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(retrieved, Is.SameAs(trackIds));
        });
    }

    // ──────────────── ReplaceLocalSearchTracks ────────────────

    [Test]
    public void ReplaceLocalSearchTracks_StoresTrackIds()
    {
        var cache = new TrackRepositoryCache();
        var tracks = new[] { MakeTrack("t1"), MakeTrack("t2") };

        cache.ReplaceLocalSearchTracks(tracks);

        Assert.That(cache.LocalSearchTrackIds, Is.EqualTo(new[] { "t1", "t2" }));
    }

    // ──────────────── ReplaceYandexSearchTracks ────────────────

    [Test]
    public void ReplaceYandexSearchTracks_StoresTrackIds()
    {
        var cache = new TrackRepositoryCache();
        var tracks = new[] { MakeTrack("y1"), MakeTrack("y2") };

        cache.ReplaceYandexSearchTracks(tracks);

        Assert.That(cache.YandexSearchTrackIds, Is.EqualTo(new[] { "y1", "y2" }));
    }

    // ──────────────── ReplaceQueueTrackIds ────────────────

    [Test]
    public void ReplaceQueueTrackIds_StoresTrackIds()
    {
        var cache = new TrackRepositoryCache();

        cache.ReplaceQueueTrackIds(new[] { "q1", "q2" });

        Assert.That(cache.QueueTrackIds, Is.EqualTo(new[] { "q1", "q2" }));
    }

    // ──────────────── ReplaceMyWaveTracks ────────────────

    [Test]
    public void ReplaceMyWaveTracks_FiresMyWaveReplacedEvent()
    {
        var cache = new TrackRepositoryCache();
        var eventFired = false;
        cache.MyWaveReplaced += () => eventFired = true;

        cache.ReplaceMyWaveTracks(new[] { MakeTrack("m1") });

        Assert.That(eventFired, Is.True);
    }

    [Test]
    public void ReplaceMyWaveTracks_ReplacesContent()
    {
        var cache = new TrackRepositoryCache();
        cache.ReplaceMyWaveTracks(new[] { MakeTrack("a"), MakeTrack("b") });
        cache.ReplaceMyWaveTracks(new[] { MakeTrack("c") });

        Assert.That(cache.MyWaveTracks.Select(t => t.Id), Is.EqualTo(new[] { "c" }));
    }

    // ──────────────── AppendMyWaveTracks ────────────────

    [Test]
    public void AppendMyWaveTracks_FiresMyWaveAppendedEvent()
    {
        var cache = new TrackRepositoryCache();
        var eventFired = false;
        cache.MyWaveAppended += () => eventFired = true;

        cache.AppendMyWaveTracks(new[] { MakeTrack("a") });

        Assert.That(eventFired, Is.True);
    }

    [Test]
    public void AppendMyWaveTracks_AppendsToExistingContent()
    {
        var cache = new TrackRepositoryCache();
        cache.ReplaceMyWaveTracks(new[] { MakeTrack("a") });
        cache.AppendMyWaveTracks(new[] { MakeTrack("b"), MakeTrack("c") });

        Assert.That(cache.MyWaveTracks.Select(t => t.Id), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    // ──────────────── Idempotent replace ────────────────

    [Test]
    public void ReplaceMyWaveTracks_ClearsBeforeReplacing()
    {
        var cache = new TrackRepositoryCache();
        cache.AppendMyWaveTracks(new[] { MakeTrack("old") });

        cache.ReplaceMyWaveTracks(new[] { MakeTrack("new") });

        Assert.That(cache.MyWaveTracks.Select(t => t.Id), Is.EqualTo(new[] { "new" }));
    }
}
