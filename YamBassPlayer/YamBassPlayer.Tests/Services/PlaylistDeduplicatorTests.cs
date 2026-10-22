using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class PlaylistDeduplicatorTests
{
    private static Playlist Yandex(string kind, string title, string ownerUid, string remoteKind)
        => new(kind, title)
        {
            SourceId = new SourceId("yandex"),
            Payload = new Dictionary<string, string>
            {
                ["owner"] = ownerUid,
                ["kind"] = remoteKind
            }
        };

    [Test]
    public void ByCacheKey_RemovesSamePlaylistFromDifferentEndpoints()
    {
        // «Плейлист дня» приходит и с лендинга, и в списке собственных плейлистов.
        var playlists = new List<Playlist>
        {
            Yandex("personal", "Плейлист дня", "42", "daily"),
            Yandex("own", "Плейлист дня", "42", "daily")
        };

        var result = PlaylistDeduplicator.ByCacheKey(playlists);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Kind, Is.EqualTo("personal"));
    }

    [Test]
    public void ByCacheKey_KeepsDifferentPlaylistsWithSameTitle()
    {
        var playlists = new List<Playlist>
        {
            Yandex("own", "Микс", "42", "1"),
            Yandex("liked", "Микс", "77", "2")
        };

        var result = PlaylistDeduplicator.ByCacheKey(playlists);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void ByCacheKey_DeduplicatesPlaylistsWithoutOwnerByCacheKey()
    {
        var playlists = new List<Playlist>
        {
            new("favorite", "Мои треки") { SourceId = new SourceId("yandex") },
            new("favorite", "Мои треки") { SourceId = new SourceId("yandex") }
        };

        var result = PlaylistDeduplicator.ByCacheKey(playlists);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void ByCacheKey_PreservesOrder()
    {
        var playlists = new List<Playlist>
        {
            new("favorite", "Мои треки") { SourceId = new SourceId("yandex") },
            Yandex("personal", "Плейлист дня", "42", "daily"),
            Yandex("own", "Мой микс", "42", "7"),
            Yandex("own", "Плейлист дня", "42", "daily")
        };

        var result = PlaylistDeduplicator.ByCacheKey(playlists);

        Assert.That(
            result.Select(p => p.Title),
            Is.EqualTo(new[] { "Мои треки", "Плейлист дня", "Мой микс" }));
    }
}
