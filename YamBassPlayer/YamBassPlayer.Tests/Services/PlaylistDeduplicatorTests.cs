using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class PlaylistDeduplicatorTests
{
    private static Playlist Yandex(
        string name,
        PlaylistType type,
        string ownerUid,
        string kind)
        => new(name, type)
        {
            SourceId = SourceIds.Yandex,
            Payload = new PlaylistPayload { YandexOwnerUid = ownerUid, YandexKind = kind }
        };

    [Test]
    public void ByYandexIdentity_RemovesSamePlaylistFromDifferentEndpoints()
    {
        // «Плейлист дня» приходит и с лендинга, и в списке собственных плейлистов.
        var playlists = new List<Playlist>
        {
            Yandex("Плейлист дня", PlaylistType.Custom, "42", "daily"),
            Yandex("Плейлист дня", PlaylistType.YandexOwnPlaylist, "42", "daily")
        };

        var result = PlaylistDeduplicator.ByYandexIdentity(playlists);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(PlaylistType.Custom));
    }

    [Test]
    public void ByYandexIdentity_KeepsDifferentPlaylistsWithSameTitle()
    {
        var playlists = new List<Playlist>
        {
            Yandex("Микс", PlaylistType.YandexOwnPlaylist, "42", "1"),
            Yandex("Микс", PlaylistType.YandexLikedPlaylist, "77", "2")
        };

        var result = PlaylistDeduplicator.ByYandexIdentity(playlists);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void ByYandexIdentity_DoesNotTouchPlaylistsWithoutYandexIds()
    {
        var playlists = new List<Playlist>
        {
            new("Мои треки", PlaylistType.Favorite) { SourceId = SourceIds.Yandex },
            new("Мои треки", PlaylistType.Favorite) { SourceId = SourceIds.Yandex }
        };

        var result = PlaylistDeduplicator.ByYandexIdentity(playlists);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void ByYandexIdentity_PreservesOrder()
    {
        var playlists = new List<Playlist>
        {
            new("Мои треки", PlaylistType.Favorite),
            Yandex("Плейлист дня", PlaylistType.Custom, "42", "daily"),
            Yandex("Мой микс", PlaylistType.YandexOwnPlaylist, "42", "7"),
            Yandex("Плейлист дня", PlaylistType.YandexOwnPlaylist, "42", "daily")
        };

        var result = PlaylistDeduplicator.ByYandexIdentity(playlists);

        Assert.That(
            result.Select(p => p.PlaylistName),
            Is.EqualTo(new[] { "Мои треки", "Плейлист дня", "Мой микс" }));
    }
}
