using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class PlaylistStateStoreTests
{
    private string _file = null!;
    private string _stateDir = null!;
    private IPluginStateFile _pluginState = null!;

    [SetUp]
    public void SetUp()
    {
        _file = Path.Combine(Path.GetTempPath(), "ybp-state-" + Guid.NewGuid().ToString("N") + ".json");
        _stateDir = Path.Combine(Path.GetTempPath(), "ybp-plugin-state-" + Guid.NewGuid().ToString("N"));
        _pluginState = new PluginStateFile(_stateDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_file))
            File.Delete(_file);

        string backup = Path.Combine(Path.GetDirectoryName(_file)!, "playlist_state.v1.json");
        if (File.Exists(backup))
            File.Delete(backup);

        if (Directory.Exists(_stateDir))
            Directory.Delete(_stateDir, true);
    }

    private static Playlist Data(string title, string kind = "favorite")
        => new(kind, title)
        {
            SourceId = new SourceId("yandex"),
            Category = PlaylistCategory.Data,
            TrackCount = 3
        };

    [Test]
    public void SaveThenLoad_RoundTripsDataPlaylists()
    {
        var store = new PlaylistStateStore(_file, _pluginState);
        store.Save(new PlaylistState { Playlists = [Data("Мои треки")] });

        var loaded = new PlaylistStateStore(_file, _pluginState).Load();

        Assert.That(loaded!.Playlists, Has.Count.EqualTo(1));
        Assert.That(loaded.Playlists[0].Title, Is.EqualTo("Мои треки"));
        Assert.That(loaded.Playlists[0].SourceId, Is.EqualTo(new SourceId("yandex")));
    }

    [Test]
    public void Load_NormalizesLegacyLocalFavoriteToRoot()
    {
        var localFavorite = new Playlist("favorite", "Локальное Избранное")
        {
            SourceId = LocalMusicSource.Id,
            Category = PlaylistCategory.Data,
            TrackCount = 2
        };
        var store = new PlaylistStateStore(_file, _pluginState);
        store.Save(new PlaylistState { Playlists = [localFavorite], LastPlaylist = localFavorite });

        var loaded = new PlaylistStateStore(_file, _pluginState).Load();

        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Playlists[0].SourceId, Is.Null);
            Assert.That(loaded.Playlists[0].Kind, Is.EqualTo("favorite"));
            Assert.That(loaded.Playlists[0].Title, Is.EqualTo("Локальное избранное"));
            Assert.That(loaded.LastPlaylist?.SourceId, Is.Null);
        });
    }

    [Test]
    public void Save_WritesOnlyDataPlaylists()
    {
        var store = new PlaylistStateStore(_file, _pluginState);
        store.Save(new PlaylistState
        {
            Playlists =
            [
                Data("Мои треки"),
                new("cached", "Загруженные") { Category = PlaylistCategory.Computed },
                new("queue", "Очередь") { Category = PlaylistCategory.Transient }
            ]
        });

        var loaded = new PlaylistStateStore(_file, _pluginState).Load();

        Assert.That(loaded!.Playlists.Select(p => p.Title), Is.EqualTo(new[] { "Мои треки" }));
    }

    [Test]
    public void Save_KeepsNonTransientLastPlaylist()
    {
        var store = new PlaylistStateStore(_file, _pluginState);
        store.Save(new PlaylistState
        {
            Playlists = [Data("Мои треки")],
            LastPlaylist = new Playlist("cached", "Загруженные") { Category = PlaylistCategory.Computed }
        });

        var loaded = new PlaylistStateStore(_file, _pluginState).Load();

        Assert.That(loaded!.LastPlaylist?.Title, Is.EqualTo("Загруженные"));
    }

    [Test]
    public void Save_DropsTransientLastPlaylist()
    {
        var store = new PlaylistStateStore(_file, _pluginState);
        store.Save(new PlaylistState
        {
            Playlists = [Data("Мои треки")],
            LastPlaylist = new Playlist("queue", "Очередь") { Category = PlaylistCategory.Transient }
        });

        var loaded = new PlaylistStateStore(_file, _pluginState).Load();

        Assert.That(loaded!.LastPlaylist, Is.Null);
    }

    [Test]
    public void SaveThenLoad_RoundTripsLastTrack()
    {
        var store = new PlaylistStateStore(_file, _pluginState);
        store.Save(new PlaylistState
        {
            Playlists = [Data("Мои треки")],
            LastTrack = new TrackKey(new SourceId("yandex"), "987654")
        });

        var loaded = new PlaylistStateStore(_file, _pluginState).Load();

        Assert.That(loaded!.LastTrack, Is.EqualTo(new TrackKey(new SourceId("yandex"), "987654")));
    }

    [Test]
    public void Save_WritesSourceIdAsString()
    {
        var store = new PlaylistStateStore(_file, _pluginState);
        store.Save(new PlaylistState { Playlists = [Data("Мои треки")] });

        string json = File.ReadAllText(_file);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"SourceId\": \"yandex\""));
            Assert.That(json, Does.Not.Contain("\"Value\""));
        });
    }

    [Test]
    public void Load_CorruptFileReturnsNull()
    {
        File.WriteAllText(_file, "{ not json");

        Assert.That(new PlaylistStateStore(_file, _pluginState).Load(), Is.Null);
    }

    [Test]
    public void Load_VersionOneMigratesToVersionTwo()
    {
        File.WriteAllText(_file, """
            {
              "Version": 1,
              "Playlists": [
                { "Name": "Мои треки", "Type": "Favorite", "TrackCount": 1 }
              ],
              "FavoriteTrackIds": [ "123" ],
              "LastPlaylist": { "Name": "Загруженные", "Type": "Cached" },
              "LastTrackId": "123"
            }
            """);

        var loaded = new PlaylistStateStore(_file, _pluginState).Load();

        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Playlists, Has.Count.EqualTo(1));
            Assert.That(loaded.Playlists[0].Kind, Is.EqualTo("favorite"));
            Assert.That(loaded.Playlists[0].SourceId, Is.EqualTo(new SourceId("yandex")));
            Assert.That(loaded.LastPlaylist?.Kind, Is.EqualTo("cached"));
            Assert.That(loaded.LastTrack, Is.EqualTo(new TrackKey(new SourceId("yandex"), "123")));
            Assert.That(File.ReadAllText(_file), Does.Contain("\"Version\": 2"));
            Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(_file)!, "playlist_state.v1.json")), Is.True);
            Assert.That(_pluginState.Get<List<string>>("yandex-music", "favoriteTrackIds"), Is.EqualTo(new List<string> { "123" }));
        });
    }
}
