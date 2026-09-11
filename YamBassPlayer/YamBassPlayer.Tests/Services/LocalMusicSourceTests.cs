using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class LocalMusicSourceTests
{
    private Mock<ILocalLibraryService> _library = null!;
    private LocalMusicSource _source = null!;

    [SetUp]
    public void SetUp()
    {
        _library = new Mock<ILocalLibraryService>();
        _source = new LocalMusicSource(_library.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);

    [Test]
    public async Task GetPlaylistsAsync_PutsFolderIdIntoTypedPayloadNotDescription()
    {
        _library.Setup(s => s.GetFoldersAsync())
            .ReturnsAsync([new LocalFolder(5, "C:\\Music", "Музыка")]);
        _library.Setup(s => s.GetTrackCountAsync(5)).ReturnsAsync(3);
        _library.Setup(s => s.GetTrackCountAsync(null)).ReturnsAsync(7);

        var playlists = (await _source.GetPlaylistsAsync()).ToList();

        var folderPlaylist = playlists.Single(p => p.Type == PlaylistType.LocalFolder);
        Assert.Multiple(() =>
        {
            Assert.That(folderPlaylist.Payload?.FolderId, Is.EqualTo(5));
            Assert.That(folderPlaylist.Description, Is.EqualTo("C:\\Music"));
            Assert.That(playlists.Select(p => p.Type), Does.Contain(PlaylistType.LocalSearch));
        });
    }

    [Test]
    public async Task GetPlaylistTracksAsync_LocalFolder_RoutesByPayloadFolderId()
    {
        _library.Setup(s => s.GetTracksAsync(5)).ReturnsAsync([Track("a")]);
        var playlist = new Playlist("Музыка", PlaylistType.LocalFolder)
        {
            Payload = new PlaylistPayload { FolderId = 5 }
        };

        var tracks = (await _source.GetPlaylistTracksAsync(playlist, 0, int.MaxValue)).ToList();

        Assert.Multiple(() =>
        {
            _library.Verify(s => s.GetTracksAsync(5), Times.Once);
            Assert.That(tracks.Select(t => t.Id), Is.EqualTo(new[] { "a" }));
        });
    }

    [Test]
    public async Task GetPlaylistTracksAsync_LocalFolderWithoutPayload_ReturnsEmpty()
    {
        var playlist = new Playlist("Музыка", PlaylistType.LocalFolder);

        var tracks = (await _source.GetPlaylistTracksAsync(playlist, 0, int.MaxValue)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(tracks, Is.Empty);
            _library.Verify(s => s.GetTracksAsync(It.IsAny<int?>()), Times.Never);
        });
    }
}
