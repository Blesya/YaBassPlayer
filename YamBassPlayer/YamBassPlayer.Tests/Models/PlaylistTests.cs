using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Tests.Models;

[TestFixture]
public sealed class PlaylistTests
{
    private static readonly SourceId Yandex = new("yandex");

    [Test]
    public void CacheKey_DefaultsToSourceKindAndTitle()
    {
        var playlist = new Playlist("favorite", "Мои треки") { SourceId = Yandex };

        Assert.That(playlist.CacheKey, Is.EqualTo("yandex:favorite:Мои треки"));
    }

    [Test]
    public void CacheKey_UsesOwnerAndKindFromPayload()
    {
        var playlist = new Playlist("own", "Мой микс")
        {
            SourceId = Yandex,
            Payload = new Dictionary<string, string>
            {
                ["owner"] = "42",
                ["kind"] = "7"
            }
        };

        Assert.That(playlist.CacheKey, Is.EqualTo("42:7"));
    }

    [Test]
    public void CacheKey_InitOverrideWins()
    {
        var playlist = new Playlist("own", "Мой микс")
        {
            SourceId = Yandex,
            CacheKey = "legacy-key"
        };

        Assert.That(playlist.CacheKey, Is.EqualTo("legacy-key"));
    }

    [Test]
    public void HasSameIdentity_ComparesSourceAndCacheKey()
    {
        var first = new Playlist("own", "Мой микс")
        {
            SourceId = Yandex,
            Payload = new Dictionary<string, string> { ["owner"] = "42", ["kind"] = "7" }
        };
        var same = new Playlist("own", "Другое имя")
        {
            SourceId = Yandex,
            Payload = new Dictionary<string, string> { ["owner"] = "42", ["kind"] = "7" }
        };
        var other = new Playlist("own", "Мой микс")
        {
            SourceId = Yandex,
            Payload = new Dictionary<string, string> { ["owner"] = "43", ["kind"] = "7" }
        };

        Assert.Multiple(() =>
        {
            Assert.That(first.HasSameIdentity(same), Is.True);
            Assert.That(first.HasSameIdentity(other), Is.False);
        });
    }

    [Test]
    public void HasSameIdentity_IgnoresKindChangeForSamePlaylist()
    {
        // Один и тот же плейлист: вчера он был сохранённым чужим (liked), сегодня
        // стал собственным (own). Прошлый выбор должен найтись по owner:kind.
        var saved = new Playlist("liked", "Спокойный плейлист")
        {
            SourceId = Yandex,
            Payload = new Dictionary<string, string> { ["owner"] = "405705740", ["kind"] = "1049" }
        };
        var fresh = new Playlist("own", "Спокойный плейлист")
        {
            SourceId = Yandex,
            Payload = new Dictionary<string, string> { ["owner"] = "405705740", ["kind"] = "1049" }
        };

        Assert.That(saved.HasSameIdentity(fresh), Is.True);
    }

    [Test]
    public void Payload_StoresStringPairs()
    {
        var playlist = new Playlist("folder", "Музыка")
        {
            Payload = new Dictionary<string, string> { ["folder-id"] = "3" }
        };

        Assert.That(playlist.Payload!["folder-id"], Is.EqualTo("3"));
    }

    [Test]
    public void ToString_ShowsTitleAndCount()
    {
        var playlist = new Playlist("favorite", "Мои треки") { TrackCount = 5 };

        Assert.That(playlist.ToString(), Is.EqualTo("Мои треки (5)"));
    }

    [Test]
    public void Category_DefaultsToData()
    {
        var playlist = new Playlist("favorite", "Мои треки");

        Assert.That(playlist.Category, Is.EqualTo(PlaylistCategory.Data));
    }

    [Test]
    public void Context_DefaultsToRegular()
    {
        var playlist = new Playlist("favorite", "Мои треки");

        Assert.That(playlist.Context, Is.EqualTo(PlaylistContext.Regular));
    }
}
