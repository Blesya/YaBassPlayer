using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class PlaylistTreeComposerTests
{
    #region Scenario 1: Empty builders and empty playlists

    [Test]
    public async Task ComposeAsync_EmptyBuildersAndEmptyPlaylists_ReturnsEmptyList()
    {
        // Arrange
        var builders = Enumerable.Empty<ITreeBranchBuilder>();
        var composer = new PlaylistTreeComposer(builders);
        var playlists = Array.Empty<Playlist>();

        // Act
        var result = await composer.ComposeAsync(playlists);

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region Scenario 2: Builder returns null branch

    [Test]
    public async Task ComposeAsync_WhenBuilderReturnsNull_NullNotAddedToResult()
    {
        // Arrange
        var mockBuilder = new Mock<ITreeBranchBuilder>();
        mockBuilder.Setup(b => b.Order).Returns(0);
        mockBuilder.Setup(b => b.IsStatic).Returns(false);
        mockBuilder.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlaylistTreeItem?)null);

        var composer = new PlaylistTreeComposer(new[] { mockBuilder.Object });
        var playlists = Array.Empty<Playlist>();

        // Act
        var result = await composer.ComposeAsync(playlists);

        // Assert
        Assert.That(result, Is.Empty);
    }

    #endregion

    #region Scenario 3: Builder returns branch – branch is added

    [Test]
    public async Task ComposeAsync_WhenBuilderReturnsBranch_BranchIsAddedToResult()
    {
        // Arrange
        var branch = new PlaylistTreeItem { Label = "TestBranch" };

        var mockBuilder = new Mock<ITreeBranchBuilder>();
        mockBuilder.Setup(b => b.Order).Returns(0);
        mockBuilder.Setup(b => b.IsStatic).Returns(false);
        mockBuilder.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        var composer = new PlaylistTreeComposer(new[] { mockBuilder.Object });
        var playlists = Array.Empty<Playlist>();

        // Act
        var result = await composer.ComposeAsync(playlists);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.SameAs(branch));
    }

    #endregion

    #region Scenario 4: Builders ordered by their Order property

    [Test]
    public async Task ComposeAsync_MultipleBuilders_BranchesAddedInOrderSequence()
    {
        // Arrange
        var branchA = new PlaylistTreeItem { Label = "A" };
        var branchB = new PlaylistTreeItem { Label = "B" };
        var branchC = new PlaylistTreeItem { Label = "C" };

        var mockBuilderA = new Mock<ITreeBranchBuilder>();
        mockBuilderA.Setup(b => b.Order).Returns(10);
        mockBuilderA.Setup(b => b.IsStatic).Returns(false);
        mockBuilderA.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(branchA);

        var mockBuilderB = new Mock<ITreeBranchBuilder>();
        mockBuilderB.Setup(b => b.Order).Returns(5);
        mockBuilderB.Setup(b => b.IsStatic).Returns(false);
        mockBuilderB.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(branchB);

        var mockBuilderC = new Mock<ITreeBranchBuilder>();
        mockBuilderC.Setup(b => b.Order).Returns(1);
        mockBuilderC.Setup(b => b.IsStatic).Returns(false);
        mockBuilderC.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(branchC);

        var composer = new PlaylistTreeComposer(new[] { mockBuilderA.Object, mockBuilderB.Object, mockBuilderC.Object });
        var playlists = Array.Empty<Playlist>();

        // Act
        var result = await composer.ComposeAsync(playlists);

        // Assert
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0].Label, Is.EqualTo("C"));
        Assert.That(result[1].Label, Is.EqualTo("B"));
        Assert.That(result[2].Label, Is.EqualTo("A"));
    }

    #endregion

    #region Scenario 5: Static builder caching

    [Test]
    public async Task ComposeAsync_StaticBuilder_FirstCallBuildsSecondCallReturnsCached()
    {
        // Arrange
        var branch = new PlaylistTreeItem { Label = "StaticBranch" };
        var callCount = 0;

        var mockBuilder = new Mock<ITreeBranchBuilder>();
        mockBuilder.Setup(b => b.Order).Returns(0);
        mockBuilder.Setup(b => b.IsStatic).Returns(true);
        mockBuilder.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return branch;
            });

        var composer = new PlaylistTreeComposer(new[] { mockBuilder.Object });
        var playlists = Array.Empty<Playlist>();

        // Act
        var result1 = await composer.ComposeAsync(playlists);
        var result2 = await composer.ComposeAsync(playlists);

        // Assert
        Assert.That(callCount, Is.EqualTo(1), "BuildBranchAsync should be called only once for a static builder");
        Assert.That(result1, Has.Count.EqualTo(1));
        Assert.That(result2, Has.Count.EqualTo(1));
        Assert.That(result1[0], Is.SameAs(branch));
        Assert.That(result2[0], Is.SameAs(branch));
    }

    #endregion

    #region Scenario 6: InvalidateCache forces re-evaluation

    [Test]
    public async Task InvalidateCache_AfterInvalidation_StaticBuilderBuildsAgain()
    {
        // Arrange
        var branch1 = new PlaylistTreeItem { Label = "First" };
        var branch2 = new PlaylistTreeItem { Label = "Second" };
        var callCount = 0;

        var mockBuilder = new Mock<ITreeBranchBuilder>();
        mockBuilder.Setup(b => b.Order).Returns(0);
        mockBuilder.Setup(b => b.IsStatic).Returns(true);
        mockBuilder.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? branch1 : branch2;
            });

        var composer = new PlaylistTreeComposer(new[] { mockBuilder.Object });
        var playlists = Array.Empty<Playlist>();

        // Act - first call builds
        var result1 = await composer.ComposeAsync(playlists);

        // Act - invalidate cache
        composer.InvalidateCache();

        // Act - second call should build again
        var result2 = await composer.ComposeAsync(playlists);

        // Assert
        Assert.That(callCount, Is.EqualTo(2), "BuildBranchAsync should be called again after cache invalidation");
        Assert.That(result1[0], Is.SameAs(branch1));
        Assert.That(result2[0], Is.SameAs(branch2));
    }

    #endregion

    #region Scenario 7: Application root playlists appended

    [Test]
    public async Task ComposeAsync_AppendsApplicationRootPlaylists()
    {
        // Arrange
        // Data category with null SourceId → appended
        var dataRootPlaylist = new Playlist("DataRoot", PlaylistType.Favorite)
        {
            SourceId = null
        };

        // Entity category → not appended (even with null SourceId)
        var entityPlaylist = new Playlist("EntityRoot", PlaylistType.Artist)
        {
            SourceId = null
        };

        // Computed category → appended (regardless of SourceId)
        var computedPlaylistWithSource = new Playlist("ComputedWithSource", PlaylistType.Top10)
        {
            SourceId = "someSource"
        };

        // Computed category with null SourceId → also appended
        var computedPlaylistNullSource = new Playlist("ComputedNullSource", PlaylistType.Cached)
        {
            SourceId = null
        };

        // Data category with non-null SourceId → not appended
        var dataWithSourcePlaylist = new Playlist("DataWithSource", PlaylistType.Custom)
        {
            SourceId = "yandex"
        };

        var playlists = new List<Playlist>
        {
            dataRootPlaylist,
            entityPlaylist,
            computedPlaylistWithSource,
            computedPlaylistNullSource,
            dataWithSourcePlaylist
        };

        var mockBuilder = new Mock<ITreeBranchBuilder>();
        mockBuilder.Setup(b => b.Order).Returns(0);
        mockBuilder.Setup(b => b.IsStatic).Returns(false);
        mockBuilder.Setup(b => b.BuildBranchAsync(It.IsAny<IReadOnlyList<Playlist>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlaylistTreeItem?)null);

        var composer = new PlaylistTreeComposer(new[] { mockBuilder.Object });

        // Act
        var result = await composer.ComposeAsync(playlists);

        // Assert - only the "root" playlists should be appended
        Assert.That(result, Has.Count.EqualTo(3));

        var rootItems = result.Select(i => i.Playlist!.PlaylistName).ToList();
        Assert.That(rootItems, Does.Contain("DataRoot"));
        Assert.That(rootItems, Does.Contain("ComputedWithSource"));
        Assert.That(rootItems, Does.Contain("ComputedNullSource"));
        Assert.That(rootItems, Does.Not.Contain("EntityRoot"));
        Assert.That(rootItems, Does.Not.Contain("DataWithSource"));
    }

    #endregion

    #region Scenario 8: CancellationToken throws on cancellation

    [Test]
    public void ComposeAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var composer = new PlaylistTreeComposer(Enumerable.Empty<ITreeBranchBuilder>());
        var playlists = Array.Empty<Playlist>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.That(
            () => composer.ComposeAsync(playlists, cts.Token),
            Throws.Exception.TypeOf<OperationCanceledException>());
    }

    #endregion

    #region Scenario 9: Null playlists throws ArgumentNullException

    [Test]
    public void ComposeAsync_NullPlaylists_ThrowsArgumentNullException()
    {
        // Arrange
        var composer = new PlaylistTreeComposer(Enumerable.Empty<ITreeBranchBuilder>());

        // Act & Assert
        Assert.That(
            () => composer.ComposeAsync(null!),
            Throws.ArgumentNullException);
    }

    #endregion
}
