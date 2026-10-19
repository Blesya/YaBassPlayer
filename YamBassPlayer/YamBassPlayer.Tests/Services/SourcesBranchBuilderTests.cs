using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class SourcesBranchBuilderTests
{
    private Mock<IMusicSourceRegistry> _registry = null!;
    private Mock<ILocalLibraryService> _localLibrary = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new Mock<IMusicSourceRegistry>();
        _localLibrary = new Mock<ILocalLibraryService>();

        var yandexSource = new Mock<IMusicSource>();
        yandexSource.SetupGet(s => s.DisplayName).Returns("Яндекс.Музыка");

        var localSource = new Mock<IMusicSource>();
        localSource.SetupGet(s => s.DisplayName).Returns("Локальная музыка");
        localSource
            .Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Playlist>());

        _localLibrary
            .Setup(l => l.GetLocalArtistsAsync(It.IsAny<int?>()))
            .ReturnsAsync(Array.Empty<(string artistName, int trackCount)>());

        _registry.Setup(r => r.GetRequired(SourceIds.Yandex)).Returns(yandexSource.Object);
        _registry.Setup(r => r.GetRequired(SourceIds.Local)).Returns(localSource.Object);
    }

    private SourcesBranchBuilder CreateBuilder()
        => new(_registry.Object, _localLibrary.Object);

    private static Playlist Yandex(
        string name,
        PlaylistType type,
        string? ownerUid = null,
        string? kind = null)
        => new(name, type)
        {
            SourceId = SourceIds.Yandex,
            TrackCount = 3,
            Payload = ownerUid is null
                ? null
                : new PlaylistPayload { YandexOwnerUid = ownerUid, YandexKind = kind }
        };

    private static PlaylistTreeItem GetYandexNode(PlaylistTreeItem root)
        => root.Children
            .OfType<PlaylistTreeItem>()
            .Single(node => Equals(node.Tag, SourceIds.Yandex));

    [Test]
    public async Task BuildBranch_OwnPlaylists_GoToOwnSubgroup()
    {
        var playlists = new List<Playlist>
        {
            Yandex("Мои треки", PlaylistType.Favorite),
            Yandex("Плейлист дня", PlaylistType.Custom),
            Yandex("Мой микс", PlaylistType.YandexOwnPlaylist, "1", "10"),
            Yandex("Чужой микс", PlaylistType.YandexLikedPlaylist, "2", "20")
        };

        var root = await CreateBuilder().BuildBranchAsync(playlists);

        var yandexNode = GetYandexNode(root!);
        var ownGroup = yandexNode.Children
            .OfType<PlaylistTreeItem>()
            .Single(node => node.Label == "Мои плейлисты");

        Assert.Multiple(() =>
        {
            Assert.That(
                ownGroup.Children.OfType<PlaylistTreeItem>().Select(node => node.Playlist!.PlaylistName),
                Is.EqualTo(new[] { "Мой микс" }));

            // Всё, что было в корне, остаётся на месте.
            Assert.That(
                yandexNode.Children.OfType<PlaylistTreeItem>().Any(node => node.Playlist?.Type == PlaylistType.Favorite),
                Is.True);
            Assert.That(
                yandexNode.Children.OfType<PlaylistTreeItem>().Any(node => node.Playlist?.Type == PlaylistType.Custom),
                Is.True);
            Assert.That(
                yandexNode.Children.OfType<PlaylistTreeItem>().Any(node => node.Playlist?.Type == PlaylistType.YandexLikedPlaylist),
                Is.True);
        });
    }

    [Test]
    public async Task BuildBranch_WithoutOwnPlaylists_DoesNotAddEmptySubgroup()
    {
        var playlists = new List<Playlist>
        {
            Yandex("Мои треки", PlaylistType.Favorite),
            Yandex("Плейлист дня", PlaylistType.Custom)
        };

        var root = await CreateBuilder().BuildBranchAsync(playlists);

        var yandexNode = GetYandexNode(root!);

        Assert.That(
            yandexNode.Children.OfType<PlaylistTreeItem>().Any(node => node.Label == "Мои плейлисты"),
            Is.False);
    }

    [Test]
    public async Task BuildBranch_KeepsFavoriteFirst()
    {
        var playlists = new List<Playlist>
        {
            Yandex("Плейлист дня", PlaylistType.Custom),
            Yandex("Мои треки", PlaylistType.Favorite),
            Yandex("Мой микс", PlaylistType.YandexOwnPlaylist, "1", "10")
        };

        var root = await CreateBuilder().BuildBranchAsync(playlists);

        var first = GetYandexNode(root!).Children.OfType<PlaylistTreeItem>().First();

        Assert.That(first.Playlist?.Type, Is.EqualTo(PlaylistType.Favorite));
    }
}
