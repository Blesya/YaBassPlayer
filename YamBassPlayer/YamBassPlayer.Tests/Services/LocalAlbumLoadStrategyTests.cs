using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class LocalAlbumLoadStrategyTests
{
    private Mock<ILocalLibraryService> _library = null!;
    private LocalAlbumLoadStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _library = new Mock<ILocalLibraryService>();
        _strategy = new LocalAlbumLoadStrategy(_library.Object);
    }

    private static Track Track(string id) => new($"T-{id}", $"A-{id}", "Album", id);

    [Test]
    public void CanHandle_OnlyLocalAlbum()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_strategy.CanHandle(PlaylistType.LocalAlbum), Is.True);
            Assert.That(_strategy.CanHandle(PlaylistType.LocalArtist), Is.False);
        });
    }

    [Test]
    public async Task LoadTrackIdsAsync_WithArtistInPayload_QueriesArtistAlbum()
    {
        _library.Setup(s => s.GetTracksByAlbumAsync("Кино", "Группа крови", null))
            .ReturnsAsync([Track("a"), Track("b")]);
        var playlist = new Playlist("Группа крови", PlaylistType.LocalAlbum)
        {
            Payload = new PlaylistPayload { ArtistName = "Кино", AlbumName = "Группа крови" }
        };

        var ids = await _strategy.LoadTrackIdsAsync(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(ids, Is.EqualTo(new[] { "a", "b" }));
            _library.Verify(s => s.GetTracksByAlbumAsync("Кино", "Группа крови", null), Times.Once);
        });
    }

    [Test]
    public async Task LoadTrackIdsAsync_WithoutArtistInPayload_QueriesByAlbumTitle()
    {
        _library.Setup(s => s.GetTracksByAlbumTitleAsync("Группа крови", null))
            .ReturnsAsync([Track("a")]);
        var playlist = new Playlist("Группа крови", PlaylistType.LocalAlbum)
        {
            Payload = new PlaylistPayload { AlbumName = "Группа крови" }
        };

        var ids = await _strategy.LoadTrackIdsAsync(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(ids, Is.EqualTo(new[] { "a" }));
            _library.Verify(s => s.GetTracksByAlbumTitleAsync("Группа крови", null), Times.Once);
        });
    }

    [Test]
    public async Task LoadTrackIdsAsync_WithoutAlbumPayload_ReturnsEmpty()
    {
        var playlist = new Playlist("Группа крови", PlaylistType.LocalAlbum);

        var ids = await _strategy.LoadTrackIdsAsync(playlist);

        Assert.Multiple(() =>
        {
            Assert.That(ids, Is.Empty);
            _library.Verify(s => s.GetTracksByAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
            _library.Verify(s => s.GetTracksByAlbumTitleAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
        });
    }
}
