namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class CoverMetadataResolverTests
{
    private const string LocalPath = @"C:\covers\cover.jpg";
    private const string RemoteUrl = "https://avatars.yandex.net/cover.jpg";

    // ──────────────── ResolveRemoteCoverUrl ────────────────

    [Test]
    public void ResolveRemoteCoverUrl_ReturnsRemoteCoverUrl_WhenProvided()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl(RemoteUrl, "cover.jpg"), Is.EqualTo(RemoteUrl));
    }

    [Test]
    public void ResolveRemoteCoverUrl_FallsBackToLegacyCoverUrl_WhenItIsNotAPath()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl(null, "cover.jpg"), Is.EqualTo("cover.jpg"));
    }

    [Test]
    public void ResolveRemoteCoverUrl_DoesNotReturnLocalPath()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl(null, LocalPath), Is.Null);
    }

    [Test]
    public void ResolveRemoteCoverUrl_ReturnsNull_WhenBothAreNull()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl(null, null), Is.Null);
    }

    // ──────────────── ResolveLocalCoverPath ────────────────

    [Test]
    public void ResolveLocalCoverPath_ReturnsLocalCoverPath_WhenProvided()
    {
        Assert.That(CoverMetadataResolver.ResolveLocalCoverPath(LocalPath, null), Is.EqualTo(LocalPath));
    }

    [Test]
    public void ResolveLocalCoverPath_FallsBackToLegacyCoverUrl_WhenItIsAPath()
    {
        Assert.That(CoverMetadataResolver.ResolveLocalCoverPath(null, LocalPath), Is.EqualTo(LocalPath));
    }

    [Test]
    public void ResolveLocalCoverPath_DoesNotReturnRemoteUrl()
    {
        Assert.That(CoverMetadataResolver.ResolveLocalCoverPath(null, RemoteUrl), Is.Null);
    }

    // ──────────────── ResolveLegacyCoverUrl ────────────────

    [Test]
    public void ResolveLegacyCoverUrl_ReturnsCoverUrl_WhenProvided()
    {
        Assert.That(
            CoverMetadataResolver.ResolveLegacyCoverUrl("cover.jpg", RemoteUrl, LocalPath),
            Is.EqualTo("cover.jpg"));
    }

    [Test]
    public void ResolveLegacyCoverUrl_FallsBackToTypedColumns()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                CoverMetadataResolver.ResolveLegacyCoverUrl(null, RemoteUrl, null),
                Is.EqualTo(RemoteUrl));
            Assert.That(
                CoverMetadataResolver.ResolveLegacyCoverUrl(null, null, LocalPath),
                Is.EqualTo(LocalPath));
        });
    }

    [Test]
    public void ResolveLegacyCoverUrl_ReturnsNull_WhenNothingIsSet()
    {
        Assert.That(CoverMetadataResolver.ResolveLegacyCoverUrl(null, null, null), Is.Null);
    }
}
