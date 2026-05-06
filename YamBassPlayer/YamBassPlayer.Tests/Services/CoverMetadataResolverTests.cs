namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class CoverMetadataResolverTests
{
    // ──────────────── IsLocalSourceType ────────────────

    [Test]
    public void IsLocalSourceType_ReturnsTrue_ForLocalLowerCase()
    {
        Assert.That(CoverMetadataResolver.IsLocalSourceType("local"), Is.True);
    }

    [Test]
    public void IsLocalSourceType_ReturnsTrue_ForLocalMixedCase()
    {
        Assert.That(CoverMetadataResolver.IsLocalSourceType("Local"), Is.True);
    }

    [Test]
    public void IsLocalSourceType_ReturnsFalse_ForYandex()
    {
        Assert.That(CoverMetadataResolver.IsLocalSourceType("yandex"), Is.False);
    }

    [Test]
    public void IsLocalSourceType_ReturnsFalse_ForEmptyString()
    {
        Assert.That(CoverMetadataResolver.IsLocalSourceType(""), Is.False);
    }

    [Test]
    public void IsLocalSourceType_ReturnsFalse_ForNull()
    {
        Assert.That(CoverMetadataResolver.IsLocalSourceType(null!), Is.False);
    }

    // ──────────────── ResolveRemoteCoverUrl ────────────────

    [Test]
    public void ResolveRemoteCoverUrl_ReturnsNull_ForLocalSource()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl("local", "cover.jpg", null), Is.Null);
    }

    [Test]
    public void ResolveRemoteCoverUrl_ReturnsCoverUrl_ForYandexWhenRemoteCoverUrlIsNull()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl("yandex", "cover.jpg", null), Is.EqualTo("cover.jpg"));
    }

    [Test]
    public void ResolveRemoteCoverUrl_ReturnsRemoteCoverUrl_WhenProvided()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl("yandex", "cover.jpg", "remote.jpg"), Is.EqualTo("remote.jpg"));
    }

    [Test]
    public void ResolveRemoteCoverUrl_ReturnsNull_ForYandexWhenBothAreNull()
    {
        Assert.That(CoverMetadataResolver.ResolveRemoteCoverUrl("yandex", null, null), Is.Null);
    }

    // ──────────────── ResolveLocalCoverPath ────────────────

    [Test]
    public void ResolveLocalCoverPath_ReturnsCoverUrl_ForLocalWhenLocalCoverPathIsNull()
    {
        Assert.That(CoverMetadataResolver.ResolveLocalCoverPath("local", "cover.jpg", null), Is.EqualTo("cover.jpg"));
    }

    [Test]
    public void ResolveLocalCoverPath_ReturnsLocalCoverPath_WhenProvided()
    {
        Assert.That(CoverMetadataResolver.ResolveLocalCoverPath("local", "cover.jpg", "local.jpg"), Is.EqualTo("local.jpg"));
    }

    [Test]
    public void ResolveLocalCoverPath_ReturnsNull_ForYandex()
    {
        Assert.That(CoverMetadataResolver.ResolveLocalCoverPath("yandex", "cover.jpg", null), Is.Null);
    }

    // ──────────────── ResolveLegacyCoverUrl ────────────────

    [Test]
    public void ResolveLegacyCoverUrl_ReturnsCoverUrl_WhenProvided()
    {
        Assert.That(CoverMetadataResolver.ResolveLegacyCoverUrl("yandex", "cover.jpg", "remote.jpg", "local.jpg"), Is.EqualTo("cover.jpg"));
    }

    [Test]
    public void ResolveLegacyCoverUrl_ReturnsRemote_ForYandexWhenNoCoverUrl()
    {
        Assert.That(CoverMetadataResolver.ResolveLegacyCoverUrl("yandex", null, "remote.jpg", null), Is.EqualTo("remote.jpg"));
    }

    [Test]
    public void ResolveLegacyCoverUrl_ReturnsNull_ForYandexWhenNoCoverUrlNorRemote()
    {
        Assert.That(CoverMetadataResolver.ResolveLegacyCoverUrl("yandex", null, null, null), Is.Null);
    }

    [Test]
    public void ResolveLegacyCoverUrl_ReturnsLocal_ForLocalWhenNoCoverUrl()
    {
        Assert.That(CoverMetadataResolver.ResolveLegacyCoverUrl("local", null, null, "local.jpg"), Is.EqualTo("local.jpg"));
    }

    [Test]
    public void ResolveLegacyCoverUrl_ReturnsNull_ForLocalWhenNoCoverUrlNorLocal()
    {
        Assert.That(CoverMetadataResolver.ResolveLegacyCoverUrl("local", null, null, null), Is.Null);
    }
}
