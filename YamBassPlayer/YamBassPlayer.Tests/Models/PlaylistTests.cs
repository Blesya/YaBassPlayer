using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Tests.Models;

[TestFixture]
public class PlaylistTests
{
    [Test]
    public void Constructor_SetsPlaylistNameAndType()
    {
        var playlist = new Playlist("My Favorites", PlaylistType.Favorite);

        Assert.Multiple(() =>
        {
            Assert.That(playlist.PlaylistName, Is.EqualTo("My Favorites"));
            Assert.That(playlist.Type, Is.EqualTo(PlaylistType.Favorite));
        });
    }

    [Test]
    public void Constructor_WithCustomType_SetsType()
    {
        var playlist = new Playlist("Custom List", PlaylistType.Custom);

        Assert.That(playlist.Type, Is.EqualTo(PlaylistType.Custom));
    }

    [Test]
    public void ToString_ReturnsNameWithTrackCount()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom) { TrackCount = 15 };

        Assert.That(playlist.ToString(), Is.EqualTo("Test (15)"));
    }

    [Test]
    public void ToString_WithZeroTrackCount()
    {
        var playlist = new Playlist("Empty", PlaylistType.Custom) { TrackCount = 0 };

        Assert.That(playlist.ToString(), Is.EqualTo("Empty (0)"));
    }

    [Test]
    public void ToString_WithDefaultTrackCount()
    {
        var playlist = new Playlist("Fresh", PlaylistType.Custom);

        Assert.That(playlist.ToString(), Is.EqualTo("Fresh (0)"));
    }

    [Test]
    public void Description_CanBeSetViaInit()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom) { Description = "A description" };

        Assert.That(playlist.Description, Is.EqualTo("A description"));
    }

    [Test]
    public void TrackCount_CanBeSetAndUpdated()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom) { TrackCount = 10 };
        Assert.That(playlist.TrackCount, Is.EqualTo(10));

        playlist.TrackCount = 25;
        Assert.That(playlist.TrackCount, Is.EqualTo(25));
    }

    [Test]
    public void DayOfWeek_CanBeSetViaInit()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom) { DayOfWeek = DayOfWeek.Monday };

        Assert.That(playlist.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
    }

    [Test]
    public void DayOfWeek_DefaultsToNull()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom);

        Assert.That(playlist.DayOfWeek, Is.Null);
    }

    [Test]
    public void SourceId_CanBeSetViaInit()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom) { SourceId = "src123" };

        Assert.That(playlist.SourceId, Is.EqualTo("src123"));
    }

    [Test]
    public void SourceId_DefaultsToNull()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom);

        Assert.That(playlist.SourceId, Is.Null);
    }

    [Test]
    public void ParentTag_CanBeSetViaInit()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom) { ParentTag = "tag1" };

        Assert.That(playlist.ParentTag, Is.EqualTo("tag1"));
    }

    [Test]
    public void ParentTag_DefaultsToNull()
    {
        var playlist = new Playlist("Test", PlaylistType.Custom);

        Assert.That(playlist.ParentTag, Is.Null);
    }
}
