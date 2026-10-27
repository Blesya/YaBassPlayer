using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class SourcesBranchBuilderTests
{
    private static readonly SourceId Yandex = new("yandex");

    private Mock<IMusicSource> _yandexSource = null!;
    private Mock<IMusicSource> _localSource = null!;
    private Mock<ILocalLibraryService> _localLibrary = null!;
    private Mock<IPlaylistRefreshStatus> _refreshStatus = null!;

    [SetUp]
    public void SetUp()
    {
        _localLibrary = new Mock<ILocalLibraryService>();
        _localLibrary
            .Setup(l => l.GetLocalArtistsAsync(It.IsAny<int?>()))
            .ReturnsAsync(Array.Empty<(string artistName, int trackCount)>());

        _refreshStatus = new Mock<IPlaylistRefreshStatus>();
        _yandexSource = CreateSource(Yandex, "Яндекс.Музыка");
        _localSource = CreateSource(LocalMusicSource.Id, "Локальная музыка");
    }

    private static Mock<IMusicSource> CreateSource(SourceId id, string displayName)
    {
        var source = new Mock<IMusicSource>();
        source.SetupGet(s => s.Id).Returns(id);
        source.SetupGet(s => s.DisplayName).Returns(displayName);
        source
            .Setup(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Playlist>());
        return source;
    }

    private SourcesBranchBuilder CreateBuilder(params IMusicSource[] sources)
        => new(Registry(sources), _localLibrary.Object, _refreshStatus.Object);

    private static IMusicSourceRegistry Registry(params IMusicSource[] sources)
    {
        var registry = new Mock<IMusicSourceRegistry>();
        registry.SetupGet(r => r.Sources).Returns(sources);
        return registry.Object;
    }

    private static Playlist YandexPlaylist(string kind, string title, string? group = null)
        => new(kind, title)
        {
            SourceId = Yandex,
            Category = PlaylistCategory.Data,
            Group = group,
            TrackCount = 3
        };

    private static PlaylistTreeItem Node(PlaylistTreeItem root, string tag)
        => root.Children.OfType<PlaylistTreeItem>().Single(node => Equals(node.Tag, tag));

    [Test]
    public async Task BuildsNodePerSource()
    {
        var root = await CreateBuilder(_yandexSource.Object, _localSource.Object).BuildBranchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Node(root!, "yandex").Label, Is.EqualTo("Яндекс.Музыка"));
            Assert.That(Node(root!, "local").Label, Is.EqualTo("Локальная музыка"));
        });
    }

    [Test]
    public async Task GroupsChildrenByGroupInOrder()
    {
        Playlist[] playlists =
        [
            YandexPlaylist("favorite", "Мои треки"),
            YandexPlaylist("own", "Мой микс", "Мои плейлисты"),
            YandexPlaylist("liked", "Чужой микс", "Мои плейлисты")
        ];

        var root = await CreateBuilder(_yandexSource.Object, _localSource.Object).BuildBranchAsync(playlists);
        var yandexNode = Node(root!, "yandex");

        Assert.Multiple(() =>
        {
            Assert.That(yandexNode.Children.OfType<PlaylistTreeItem>().First().Label, Is.EqualTo("Мои треки"));
            var group = yandexNode.Children.OfType<PlaylistTreeItem>().Single(node => node.Label == "Мои плейлисты");
            Assert.That(group.Children.OfType<PlaylistTreeItem>().Select(node => node.Playlist!.Title),
                Is.EqualTo(new[] { "Мой микс", "Чужой микс" }));
        });
        _yandexSource.Verify(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UsesSnapshotPlaylistsWhenProvided()
    {
        var snapshot = new List<Playlist> { YandexPlaylist("favorite", "Мои треки") };

        var root = await CreateBuilder(_yandexSource.Object, _localSource.Object).BuildBranchAsync(snapshot);

        Assert.That(
            Node(root!, "yandex").Children.OfType<PlaylistTreeItem>().Single().Playlist!.Title,
            Is.EqualTo("Мои треки"));
        _yandexSource.Verify(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LocalArtistNodesCarryLocalSource()
    {
        _localLibrary
            .Setup(l => l.GetLocalArtistsAsync(It.IsAny<int?>()))
            .ReturnsAsync(new[] { ("Кино", 4) });
        _localLibrary
            .Setup(l => l.GetLocalAlbumsAsync("Кино", It.IsAny<int?>()))
            .ReturnsAsync(Array.Empty<(string albumName, int trackCount)>());
        _localLibrary
            .Setup(l => l.GetAllLocalAlbumsAsync(It.IsAny<int?>()))
            .ReturnsAsync(Array.Empty<(string albumName, int trackCount)>());

        var root = await CreateBuilder(_yandexSource.Object, _localSource.Object).BuildBranchAsync();
        var localNode = Node(root!, "local");
        var artistsRoot = localNode.Children.OfType<PlaylistTreeItem>().Single(node => node.Label == "Исполнители");

        Assert.That(
            artistsRoot.Children.OfType<PlaylistTreeItem>().Single().Playlist!.SourceId,
            Is.EqualTo(LocalMusicSource.Id));
    }

    [Test]
    public async Task RequiresSetup_ShowsHintAndSkipsFetching()
    {
        _yandexSource.SetupGet(s => s.State).Returns(SourceState.RequiresSetup);

        var root = await CreateBuilder(_yandexSource.Object, _localSource.Object).BuildBranchAsync();
        var yandexNode = Node(root!, "yandex");

        Assert.Multiple(() =>
        {
            Assert.That(
                yandexNode.Children.OfType<PlaylistTreeItem>().Single().Label,
                Is.EqualTo("Настройте источник"));
            _yandexSource.Verify(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task RequiresSetup_WithSnapshot_ShowsCachedPlaylists()
    {
        _yandexSource.SetupGet(s => s.State).Returns(SourceState.RequiresSetup);
        Playlist[] snapshot = [YandexPlaylist("favorite", "Мои треки")];

        var root = await CreateBuilder(_yandexSource.Object, _localSource.Object).BuildBranchAsync(snapshot);
        var yandexNode = Node(root!, "yandex");

        Assert.Multiple(() =>
        {
            Assert.That(
                yandexNode.Children.OfType<PlaylistTreeItem>().Single().Playlist!.Title,
                Is.EqualTo("Мои треки"));
            Assert.That(yandexNode.IsStale, Is.True);
            Assert.That(yandexNode.Text, Does.Contain("кеш"));
            _yandexSource.Verify(s => s.GetPlaylistsAsync(It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Test]
    public async Task ShowsAllLocalPlaylistAfterRestart()
    {
        var snapshot = new List<Playlist>
        {
            new("all", "Вся локальная музыка")
            {
                SourceId = LocalMusicSource.Id,
                Category = PlaylistCategory.Data
            }
        };

        var root = await CreateBuilder(_yandexSource.Object, _localSource.Object).BuildBranchAsync(snapshot);

        Assert.That(
            Node(root!, "local").Children.OfType<PlaylistTreeItem>().Any(node => node.Playlist?.Title == "Вся локальная музыка"),
            Is.True);
    }
}
