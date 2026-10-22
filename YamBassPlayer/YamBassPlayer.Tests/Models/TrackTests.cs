using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Tests.Models;

[TestFixture]
public class TrackTests
{
    private static readonly SourceId TestSourceId = new("test");

    [Test]
    public void Constructor_SetsTitleArtistAlbumAndId()
    {
        var track = new Track("Song Title", "Artist Name", "Album Name", "track123") { SourceId = TestSourceId };

        Assert.Multiple(() =>
        {
            Assert.That(track.Title, Is.EqualTo("Song Title"));
            Assert.That(track.Artist, Is.EqualTo("Artist Name"));
            Assert.That(track.Album, Is.EqualTo("Album Name"));
            Assert.That(track.Id, Is.EqualTo("track123"));
        });
    }

    [Test]
    public void ToString_ReturnsArtistEmDashTitle()
    {
        var track = new Track("Hello", "Adele", "25", "id1") { SourceId = TestSourceId };

        Assert.That(track.ToString(), Is.EqualTo("Adele — Hello"));
    }

    [Test]
    public void LocalFilePath_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.LocalFilePath, Is.Null);
    }

    [Test]
    public void LocalFilePath_CanBeSetViaInit()
    {
        var track = new Track("Title", "Artist", "Album", "id1")
        {
            SourceId = TestSourceId,
            LocalFilePath = @"C:\music\track.mp3"
        };

        Assert.That(track.LocalFilePath, Is.EqualTo(@"C:\music\track.mp3"));
    }

    [Test]
    public void Subtitle_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.Subtitle, Is.Null);
    }

    [Test]
    public void Subtitle_CanBeSetViaInit()
    {
        var track = new Track("Title", "Artist", "Album", "id1")
        {
            SourceId = TestSourceId,
            Subtitle = "feat. Someone"
        };

        Assert.That(track.Subtitle, Is.EqualTo("feat. Someone"));
    }

    [Test]
    public void DurationMs_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.DurationMs, Is.Null);
    }

    [Test]
    public void DurationMs_CanBeSetViaInit()
    {
        var track = new Track("Title", "Artist", "Album", "id1")
        {
            SourceId = TestSourceId,
            DurationMs = 234567
        };

        Assert.That(track.DurationMs, Is.EqualTo(234567));
    }

    [Test]
    public void Year_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.Year, Is.Null);
    }

    [Test]
    public void Year_CanBeSetViaInit()
    {
        var track = new Track("Title", "Artist", "Album", "id1")
        {
            SourceId = TestSourceId,
            Year = 2024
        };

        Assert.That(track.Year, Is.EqualTo(2024));
    }

    [Test]
    public void TrackNumber_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.TrackNumber, Is.Null);
    }

    [Test]
    public void TrackNumber_CanBeSetViaInit()
    {
        var track = new Track("Title", "Artist", "Album", "id1")
        {
            SourceId = TestSourceId,
            TrackNumber = 3
        };

        Assert.That(track.TrackNumber, Is.EqualTo(3));
    }

    [Test]
    public void CoverUrl_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.CoverUrl, Is.Null);
    }

    [Test]
    public void RemoteCoverUrl_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.RemoteCoverUrl, Is.Null);
    }

    [Test]
    public void LocalCoverPath_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.LocalCoverPath, Is.Null);
    }

    [Test]
    public void Genres_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.Genres, Is.Null);
    }

    [Test]
    public void SourceId_IsRequiredAndRoundTrips()
    {
        var track = new Track("Title", "Artist", "Album", "id1")
        {
            SourceId = new SourceId("local")
        };

        Assert.That(track.SourceId, Is.EqualTo(new SourceId("local")));
    }

    [Test]
    public void Key_CombinesSourceAndId()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.Key, Is.EqualTo(new TrackKey(TestSourceId, "id1")));
    }

    [Test]
    public void Artists_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.Artists, Is.Null);
    }

    [Test]
    public void AlbumInfo_DefaultsToNull()
    {
        var track = new Track("Title", "Artist", "Album", "id1") { SourceId = TestSourceId };

        Assert.That(track.AlbumInfo, Is.Null);
    }
}
