using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Tests.Models;

[TestFixture]
public class PlaylistTreeItemTests
{
    [Test]
    public void FromPlaylist_CreatesItemWithCorrectLabelAndText()
    {
        var playlist = new Playlist("personal", "My Playlist") { TrackCount = 42 };

        var item = PlaylistTreeItem.FromPlaylist(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(item.Label, Is.EqualTo("My Playlist"));
            Assert.That(item.Text, Is.EqualTo("My Playlist (42)"));
            Assert.That(item.Playlist, Is.SameAs(playlist));
            Assert.That(item.Tag, Is.SameAs(playlist));
            Assert.That(item.IsGroup, Is.False);
            Assert.That(item.Group, Is.Null);
        });
    }

    [Test]
    public void FromPlaylist_WithTrackCountZero_UsesZeroInText()
    {
        var playlist = new Playlist("personal", "Empty") { TrackCount = 0 };

        var item = PlaylistTreeItem.FromPlaylist(playlist);

        Assert.That(item.Text, Is.EqualTo("Empty (0)"));
    }

    [Test]
    public void FromGroup_CreatesItemWithCorrectChildrenAndText()
    {
        var child1 = new Playlist("personal", "Child1") { TrackCount = 5 };
        var child2 = new Playlist("personal", "Child2") { TrackCount = 10 };
        var group = new PlaylistGroup("Group Name", new List<Playlist> { child1, child2 }, isExpanded: true);

        var item = PlaylistTreeItem.FromGroup(group);

        Assert.Multiple(() =>
        {
            Assert.That(item.Label, Is.EqualTo("Group Name"));
            Assert.That(item.Text, Is.EqualTo("Group Name [2]"));
            Assert.That(item.Group, Is.SameAs(group));
            Assert.That(item.Tag, Is.SameAs(group));
            Assert.That(item.IsGroup, Is.True);
            Assert.That(item.IsExpandedByDefault, Is.True);
            Assert.That(item.Children, Has.Count.EqualTo(2));
            Assert.That(item.Playlist, Is.Null);
        });

        // Verify children are PlaylistTreeItem instances with correct values
        var firstChild = (PlaylistTreeItem)item.Children[0];
        var secondChild = (PlaylistTreeItem)item.Children[1];
        Assert.Multiple(() =>
        {
            Assert.That(firstChild.Label, Is.EqualTo("Child1"));
            Assert.That(firstChild.Text, Is.EqualTo("Child1 (5)"));
            Assert.That(secondChild.Label, Is.EqualTo("Child2"));
            Assert.That(secondChild.Text, Is.EqualTo("Child2 (10)"));
        });
    }

    [Test]
    public void FromGroup_WithZeroChildren_SetsTextToLabel()
    {
        var group = new PlaylistGroup("Empty Group", Array.Empty<Playlist>());

        var item = PlaylistTreeItem.FromGroup(group);

        Assert.That(item.Text, Is.EqualTo("Empty Group"));
    }

    [Test]
    public void FromGroup_ExpandedDefault_FalseByDefault()
    {
        var group = new PlaylistGroup("Group", Array.Empty<Playlist>());

        var item = PlaylistTreeItem.FromGroup(group);

        Assert.That(item.IsExpandedByDefault, Is.False);
    }

    [Test]
    public void UpdateText_WhenPlaylistIsSet_UpdatesTextWithTrackCount()
    {
        var playlist = new Playlist("personal", "Test") { TrackCount = 7 };
        var item = PlaylistTreeItem.FromPlaylist(playlist);

        playlist.TrackCount = 99;
        item.UpdateText();

        Assert.That(item.Text, Is.EqualTo("Test (99)"));
    }

    [Test]
    public void UpdateText_WhenPlaylistIsPlaying_IncludesPlayingPrefix()
    {
        var playlist = new Playlist("personal", "Now Playing") { TrackCount = 3 };
        var item = PlaylistTreeItem.FromPlaylist(playlist);
        item.IsPlaying = true;

        item.UpdateText();

        Assert.That(item.Text, Is.EqualTo("▶ Now Playing (3)"));
    }

    [Test]
    public void UpdateText_WhenPlaylistIsPlayingSetFalse_RemovesPlayingPrefix()
    {
        var playlist = new Playlist("personal", "Test") { TrackCount = 3 };
        var item = PlaylistTreeItem.FromPlaylist(playlist);
        item.IsPlaying = true;
        item.UpdateText();

        item.IsPlaying = false;
        item.UpdateText();

        Assert.That(item.Text, Is.EqualTo("Test (3)"));
    }

    [Test]
    public void UpdateText_WhenPlaylistIsNull_UsesChildCount()
    {
        var child1 = new Playlist("custom", "A");
        var child2 = new Playlist("custom", "B");
        var group = new PlaylistGroup("G", new List<Playlist> { child1, child2 });
        var item = PlaylistTreeItem.FromGroup(group);

        // UpdateText is called during FromGroup, verify initial state
        Assert.That(item.Text, Is.EqualTo("G [2]"));
    }

    [TestCase("favorite")]
    [TestCase("playlist-of-the-day")]
    [TestCase("cached")]
    [TestCase("top10")]
    [TestCase("queue")]
    [TestCase("my-wave")]
    [TestCase("folder")]
    public void FromPlaylist_TextIsJustNameAndCount(string kind)
    {
        var playlist = new Playlist(kind, "P") { TrackCount = 1 };

        var item = PlaylistTreeItem.FromPlaylist(playlist);

        Assert.That(item.Text, Is.EqualTo("P (1)"));
    }

    [Test]
    public void FromGroup_WithSourceTag_TextIsJustNameAndCount()
    {
        var group = new PlaylistGroup("Локальная музыка", new List<Playlist> { new("custom", "P") });
        var item = PlaylistTreeItem.FromGroup(group);
        item.Tag = new SourceId("local");

        item.UpdateText();

        Assert.That(item.Text, Is.EqualTo("Локальная музыка [1]"));
    }

    [Test]
    public void IsGroup_ReturnsTrue_WhenGroupIsNotNull()
    {
        var group = new PlaylistGroup("G", Array.Empty<Playlist>());
        var item = PlaylistTreeItem.FromGroup(group);

        Assert.That(item.IsGroup, Is.True);
    }

    [Test]
    public void IsGroup_ReturnsFalse_WhenGroupIsNull()
    {
        var playlist = new Playlist("personal", "P");
        var item = PlaylistTreeItem.FromPlaylist(playlist);

        Assert.That(item.IsGroup, Is.False);
    }
}
