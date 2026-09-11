using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class PlaylistStateStoreTests
{
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"playlist_state_{Guid.NewGuid():N}.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    // ──────────────── Load ────────────────

    [Test]
    public void Load_WhenFileMissing_ReturnsNull()
    {
        var store = new PlaylistStateStore(_path);

        Assert.That(store.Load(), Is.Null);
    }

    [Test]
    public void Load_WhenFileCorrupted_ReturnsNull()
    {
        File.WriteAllText(_path, "{ this is not json");
        var store = new PlaylistStateStore(_path);

        Assert.That(store.Load(), Is.Null);
    }

    [Test]
    public void Load_WhenVersionMismatch_ReturnsNull()
    {
        File.WriteAllText(_path, """{ "Version": 99, "Playlists": [] }""");
        var store = new PlaylistStateStore(_path);

        Assert.That(store.Load(), Is.Null);
    }

    // ──────────────── Round-trip ────────────────

    [Test]
    public void SaveThenLoad_RoundTripsPlaylists()
    {
        var playlist = new Playlist("Мои треки", PlaylistType.Favorite)
        {
            Description = "Треки, которые вам понравились",
            TrackCount = 42,
            SourceId = SourceIds.Yandex,
            ParentTag = "tag"
        };
        var store = new PlaylistStateStore(_path);
        store.Save(new PlaylistState { Playlists = [playlist] });

        var loaded = store.Load();

        Assert.That(loaded, Is.Not.Null);
        var restored = loaded!.Playlists.Single();
        Assert.Multiple(() =>
        {
            Assert.That(restored.PlaylistName, Is.EqualTo("Мои треки"));
            Assert.That(restored.Type, Is.EqualTo(PlaylistType.Favorite));
            Assert.That(restored.Description, Is.EqualTo("Треки, которые вам понравились"));
            Assert.That(restored.TrackCount, Is.EqualTo(42));
            Assert.That(restored.SourceId, Is.EqualTo(SourceIds.Yandex));
            Assert.That(restored.ParentTag, Is.EqualTo("tag"));
        });
    }

    [Test]
    public void SaveThenLoad_RoundTripsDayOfWeekAndPayload()
    {
        var playlist = new Playlist("Среда", PlaylistType.TopByDay)
        {
            DayOfWeek = DayOfWeek.Wednesday,
            Payload = new PlaylistPayload { FolderId = 7, ArtistName = "A", AlbumName = "B" }
        };
        var store = new PlaylistStateStore(_path);
        store.Save(new PlaylistState { Playlists = [playlist] });

        var restored = store.Load()!.Playlists.Single();

        Assert.Multiple(() =>
        {
            Assert.That(restored.DayOfWeek, Is.EqualTo(DayOfWeek.Wednesday));
            Assert.That(restored.Payload, Is.Not.Null);
            Assert.That(restored.Payload!.FolderId, Is.EqualTo(7));
            Assert.That(restored.Payload.ArtistName, Is.EqualTo("A"));
            Assert.That(restored.Payload.AlbumName, Is.EqualTo("B"));
        });
    }

    [Test]
    public void SaveThenLoad_RoundTripsTrackIdCaches()
    {
        var store = new PlaylistStateStore(_path);
        store.Save(new PlaylistState
        {
            FavoriteTrackIds = ["f1", "f2"],
            CustomPlaylistTrackIds = new Dictionary<string, List<string>>
            {
                ["Мой плейлист"] = ["c1", "c2", "c3"]
            }
        });

        var loaded = store.Load();

        Assert.Multiple(() =>
        {
            Assert.That(loaded!.FavoriteTrackIds, Is.EqualTo(new[] { "f1", "f2" }));
            Assert.That(loaded.CustomPlaylistTrackIds["Мой плейлист"], Is.EqualTo(new[] { "c1", "c2", "c3" }));
        });
    }

    [Test]
    public void SaveThenLoad_RoundTripsLastPlaylist()
    {
        var last = new Playlist("Топ 10", PlaylistType.Top10) { TrackCount = 10 };
        var store = new PlaylistStateStore(_path);
        store.Save(new PlaylistState { Playlists = [last], LastPlaylist = last });

        var restored = store.Load()!.LastPlaylist;

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.PlaylistName, Is.EqualTo("Топ 10"));
            Assert.That(restored.Type, Is.EqualTo(PlaylistType.Top10));
            Assert.That(restored.TrackCount, Is.EqualTo(10));
        });
    }

    [Test]
    public void SaveThenLoad_PersistsLastPlaylistNull()
    {
        var store = new PlaylistStateStore(_path);
        store.Save(new PlaylistState { Playlists = [new Playlist("P", PlaylistType.Cached)] });

        Assert.That(store.Load()!.LastPlaylist, Is.Null);
    }

    [Test]
    public void Save_CreatesMissingDirectory()
    {
        string nested = Path.Combine(
            Path.GetTempPath(),
            $"playlist_state_dir_{Guid.NewGuid():N}",
            "state.json");
        try
        {
            var store = new PlaylistStateStore(nested);
            store.Save(new PlaylistState { Playlists = [new Playlist("P", PlaylistType.Cached)] });

            Assert.That(File.Exists(nested), Is.True);
        }
        finally
        {
            var dir = Path.GetDirectoryName(nested)!;
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
