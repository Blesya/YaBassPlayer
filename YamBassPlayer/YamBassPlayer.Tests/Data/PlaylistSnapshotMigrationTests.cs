using System.Text.Json;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl.Migrations;

namespace YamBassPlayer.Tests.Data;

[TestFixture]
public sealed class PlaylistSnapshotMigrationTests
{
    [TestCase(LegacyPlaylistType.Favorite, "yandex", "favorite", PlaylistCategory.Data)]
    [TestCase(LegacyPlaylistType.PlaylistOfTheDaily, "yandex", "playlist-of-the-day", PlaylistCategory.Data)]
    [TestCase(LegacyPlaylistType.Custom, "yandex", "personal", PlaylistCategory.Data)]
    [TestCase(LegacyPlaylistType.YandexOwnPlaylist, "yandex", "own", PlaylistCategory.Data)]
    [TestCase(LegacyPlaylistType.YandexLikedPlaylist, "yandex", "liked", PlaylistCategory.Data)]
    [TestCase(LegacyPlaylistType.LocalFavorite, "local", "favorite", PlaylistCategory.Data)]
    [TestCase(LegacyPlaylistType.LocalFolder, "local", "folder", PlaylistCategory.Data)]
    [TestCase(LegacyPlaylistType.LocalArtist, "local", "artist", PlaylistCategory.Entity)]
    [TestCase(LegacyPlaylistType.LocalAlbum, "local", "album", PlaylistCategory.Entity)]
    [TestCase(LegacyPlaylistType.Artist, null, "artist", PlaylistCategory.Entity)]
    [TestCase(LegacyPlaylistType.Queue, null, "queue", PlaylistCategory.Transient)]
    [TestCase(LegacyPlaylistType.Cached, null, "cached", PlaylistCategory.Computed)]
    [TestCase(LegacyPlaylistType.Top10, null, "top10", PlaylistCategory.Computed)]
    [TestCase(LegacyPlaylistType.TopEvenings, null, "top-evenings", PlaylistCategory.Computed)]
    [TestCase(LegacyPlaylistType.TopByDay, null, "top-by-day", PlaylistCategory.Computed)]
    [TestCase(LegacyPlaylistType.YandexSearch, "yandex", "search", PlaylistCategory.Transient)]
    [TestCase(LegacyPlaylistType.MyWave, "yandex", "my-wave", PlaylistCategory.Transient)]
    [TestCase(LegacyPlaylistType.LocalSearch, "local", "search", PlaylistCategory.Transient)]
    public void MapLegacyType_ReturnsExpectedTriple(
        LegacyPlaylistType type,
        string? sourceId,
        string kind,
        PlaylistCategory category)
    {
        (SourceId? actualSource, string actualKind, PlaylistCategory actualCategory) =
            PlaylistSnapshotMigration.MapLegacyType(type);

        Assert.Multiple(() =>
        {
            Assert.That(actualSource, Is.EqualTo(sourceId is null ? null : new SourceId(sourceId)));
            Assert.That(actualKind, Is.EqualTo(kind));
            Assert.That(actualCategory, Is.EqualTo(category));
        });
    }

    [Test]
    public void TryMigrate_KeepsOnlyDataAndMapsPayload()
    {
        using var document = JsonDocument.Parse("""
            {
              "Version": 1,
              "Playlists": [
                {
                  "Name": "Мой микс",
                  "Type": "YandexOwnPlaylist",
                  "TrackCount": 42,
                  "Payload": { "YandexOwnerUid": "123", "YandexKind": "10" }
                },
                { "Name": "Очередь", "Type": "Queue" },
                { "Name": "Загруженные", "Type": "Cached" }
              ],
              "LastPlaylist": { "Name": "Загруженные", "Type": "Cached" },
              "LastTrackId": "987"
            }
            """);

        bool migrated = PlaylistSnapshotMigration.TryMigrate(document, out MigratedSnapshot? snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(migrated, Is.True);
            Assert.That(snapshot!.State.Playlists, Has.Count.EqualTo(1));
            Assert.That(snapshot.State.Playlists[0].CacheKey, Is.EqualTo("123:10"));
            Assert.That(snapshot.State.Playlists[0].Title, Is.EqualTo("Мой микс"));
            Assert.That(snapshot.State.LastPlaylist?.Kind, Is.EqualTo("cached"));
            Assert.That(snapshot.State.LastTrack, Is.EqualTo(new TrackKey(new SourceId("yandex"), "987")));
        });
    }

    [Test]
    public void TryMigrate_UnknownTypeIsSkipped()
    {
        using var document = JsonDocument.Parse("""
            { "Version": 1, "Playlists": [ { "Name": "X", "Type": "SomethingNew" } ] }
            """);

        bool migrated = PlaylistSnapshotMigration.TryMigrate(document, out MigratedSnapshot? snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(migrated, Is.True);
            Assert.That(snapshot!.State.Playlists, Is.Empty);
        });
    }
}
