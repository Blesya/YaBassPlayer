namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class TrackRepositoryCacheTests
{
    private static readonly SourceId Yandex = YandexMusicSourceId.Value;

    private static Track MakeTrack(string id, SourceId? source = null)
        => new("Title", "Artist", "Album", id) { SourceId = source ?? Yandex };

    private static TrackKey K(string id, SourceId? source = null)
        => new(source ?? Yandex, id);

    // ──────────────── Initial state ────────────────

    [Test]
    public void InitialState_AllCollectionsAreEmpty()
    {
        var cache = new TrackRepositoryCache();

        Assert.Multiple(() =>
        {
            Assert.That(cache.QueueTrackIds, Is.Empty);
            Assert.That(cache.MyWaveTracks, Is.Empty);
            Assert.That(cache.TryGetSearchTrackIds(Yandex, out var yandexKeys), Is.False);
            Assert.That(yandexKeys, Is.Empty);
            Assert.That(cache.TryGetSearchTrackIds(LocalMusicSource.Id, out var localKeys), Is.False);
            Assert.That(localKeys, Is.Empty);
        });
    }

    // ──────────────── Search cache ────────────────

    [Test]
    public void ReplaceSearchTracks_StoresKeysPerSource()
    {
        var cache = new TrackRepositoryCache();
        var tracks = new[] { MakeTrack("y1"), MakeTrack("y2") };

        cache.ReplaceSearchTracks(Yandex, tracks);

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGetSearchTrackIds(Yandex, out var keys), Is.True);
            Assert.That(keys, Is.EqualTo(new[] { K("y1"), K("y2") }));
            Assert.That(cache.TryGetSearchTrackIds(LocalMusicSource.Id, out _), Is.False);
        });
    }

    [Test]
    public void ReplaceSearchTracks_KeepsSourcesIndependent()
    {
        var cache = new TrackRepositoryCache();

        cache.ReplaceSearchTracks(Yandex, new[] { MakeTrack("y1") });
        cache.ReplaceSearchTracks(LocalMusicSource.Id, new[] { MakeTrack("/music/l1", LocalMusicSource.Id) });

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGetSearchTrackIds(Yandex, out var yandexKeys), Is.True);
            Assert.That(yandexKeys, Is.EqualTo(new[] { K("y1") }));
            Assert.That(cache.TryGetSearchTrackIds(LocalMusicSource.Id, out var localKeys), Is.True);
            Assert.That(localKeys, Is.EqualTo(new[] { K("/music/l1", LocalMusicSource.Id) }));
        });
    }

    // ──────────────── Playlist composition cache ────────────────

    [Test]
    public void TryGetPlaylistTrackIds_ReturnsFalse_Initially()
    {
        var cache = new TrackRepositoryCache();

        var found = cache.TryGetPlaylistTrackIds(Yandex, "42:7", out var keys);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(keys, Is.Empty);
        });
    }

    [Test]
    public void SetPlaylistTrackIds_KeepsPlaylistsIndependent()
    {
        var cache = new TrackRepositoryCache();

        cache.SetPlaylistTrackIds(Yandex, "42:7", new[] { K("1"), K("2") });
        cache.SetPlaylistTrackIds(Yandex, "42:8", new[] { K("3") });

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGetPlaylistTrackIds(Yandex, "42:7", out var own), Is.True);
            Assert.That(own, Is.EqualTo(new[] { K("1"), K("2") }));
            Assert.That(cache.TryGetPlaylistTrackIds(Yandex, "42:8", out var liked), Is.True);
            Assert.That(liked, Is.EqualTo(new[] { K("3") }));
        });
    }

    [Test]
    public void SetPlaylistTrackIds_ReplacesContentOfSameKey()
    {
        var cache = new TrackRepositoryCache();
        cache.SetPlaylistTrackIds(Yandex, "42:7", new[] { K("1"), K("2") });

        cache.SetPlaylistTrackIds(Yandex, "42:7", new[] { K("3") });

        Assert.That(cache.TryGetPlaylistTrackIds(Yandex, "42:7", out var keys), Is.True);
        Assert.That(keys, Is.EqualTo(new[] { K("3") }));
    }

    [Test]
    public void TryGetPlaylistTrackIds_ReturnsSnapshot()
    {
        var cache = new TrackRepositoryCache();
        cache.SetPlaylistTrackIds(Yandex, "42:7", new[] { K("1") });

        cache.TryGetPlaylistTrackIds(Yandex, "42:7", out var keys);
        cache.SetPlaylistTrackIds(Yandex, "42:7", new[] { K("2") });

        Assert.That(keys, Is.EqualTo(new[] { K("1") }));
    }

    [Test]
    public void ClearPlaylistTrackIds_DropsOnlyRequestedSource()
    {
        var cache = new TrackRepositoryCache();
        cache.SetPlaylistTrackIds(Yandex, "42:7", new[] { K("1") });
        cache.SetPlaylistTrackIds(LocalMusicSource.Id, "42:7", new[] { K("/music/1", LocalMusicSource.Id) });

        cache.ClearPlaylistTrackIds(Yandex);

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGetPlaylistTrackIds(Yandex, "42:7", out _), Is.False);
            Assert.That(cache.TryGetPlaylistTrackIds(LocalMusicSource.Id, "42:7", out _), Is.True);
        });
    }

    // ──────────────── ReplaceQueueTrackIds ────────────────

    [Test]
    public void ReplaceQueueTrackIds_StoresTrackKeys()
    {
        var cache = new TrackRepositoryCache();

        cache.ReplaceQueueTrackIds(new[] { K("q1"), K("q2") });

        Assert.That(cache.QueueTrackIds, Is.EqualTo(new[] { K("q1"), K("q2") }));
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
