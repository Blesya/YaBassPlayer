namespace YamBassPlayer.Tests.Services;

using Moq;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class MusicSourceRegistryTests
{
    private static Mock<IMusicSource> CreateSourceMock(string sourceId, string displayName)
    {
        var mock = new Mock<IMusicSource>();
        mock.Setup(s => s.SourceId).Returns(sourceId);
        mock.Setup(s => s.DisplayName).Returns(displayName);
        return mock;
    }

    // ──────────────── Constructor stores sources ────────────────

    [Test]
    public void Constructor_StoresSources_MakesThemAccessibleViaSources()
    {
        var source1 = CreateSourceMock("local", "Local Files").Object;
        var source2 = CreateSourceMock("yandex", "Yandex Music").Object;
        var sources = new[] { source1, source2 };

        var registry = new MusicSourceRegistry(sources);

        Assert.That(registry.Sources, Is.EquivalentTo(sources));
    }

    // ──────────────── Get() returns correct source ────────────────

    [Test]
    public void Get_ReturnsCorrectSourceByKnownId()
    {
        var local = CreateSourceMock("local", "Local Files").Object;
        var yandex = CreateSourceMock("yandex", "Yandex Music").Object;
        var registry = new MusicSourceRegistry(new[] { local, yandex });

        var result = registry.Get("yandex");

        Assert.That(result, Is.SameAs(yandex));
    }

    // ──────────────── Get() returns null for unknown ────────────────

    [Test]
    public void Get_ReturnsNull_ForUnknownId()
    {
        var local = CreateSourceMock("local", "Local Files").Object;
        var registry = new MusicSourceRegistry(new[] { local });

        Assert.That(registry.Get("nonexistent"), Is.Null);
    }

    // ──────────────── GetRequired() returns source ────────────────

    [Test]
    public void GetRequired_ReturnsSource_ForKnownId()
    {
        var local = CreateSourceMock("local", "Local Files").Object;
        var registry = new MusicSourceRegistry(new[] { local });

        var result = registry.GetRequired("local");

        Assert.That(result, Is.SameAs(local));
    }

    // ──────────────── GetRequired() throws for unknown ────────────────

    [Test]
    public void GetRequired_ThrowsInvalidOperationException_ForUnknownId()
    {
        var local = CreateSourceMock("local", "Local Files").Object;
        var registry = new MusicSourceRegistry(new[] { local });

        var ex = Assert.Throws<InvalidOperationException>(() => registry.GetRequired("unknown"));
        Assert.That(ex!.Message, Does.Contain("unknown"));
    }

    // ──────────────── Duplicate IDs ────────────────

    [Test]
    public void Constructor_ThrowsInvalidOperationException_WhenDuplicateIdsAreRegistered()
    {
        var dup1 = CreateSourceMock("dup", "First").Object;
        var dup2 = CreateSourceMock("dup", "Second").Object;

        var ex = Assert.Throws<InvalidOperationException>(() => new MusicSourceRegistry(new[] { dup1, dup2 }));
        Assert.That(ex!.Message, Does.Contain("dup"));
    }

    // ──────────────── Null sources argument ────────────────

    [Test]
    public void Constructor_ThrowsArgumentNullException_WhenSourcesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MusicSourceRegistry(null!));
    }
}
