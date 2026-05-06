namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class TrackSourceDetectorTests
{
    private static readonly TrackSourceDetector Detector = new();

    [Test]
    public void IsLocal_ReturnsTrue_ForRootedLocalPaths(
        [Values(
            @"C:\music\track.mp3",
            @"D:\songs\track.flac",
            @"\\server\share\track.mp3"
        )] string trackId)
    {
        Assert.That(Detector.IsLocal(trackId), Is.True);
    }

    [Test]
    public void IsLocal_ReturnsFalse_ForNullEmptyRelativeAndYandexIds(
        [Values(
            null,
            "",
            "relative\\path\\track.mp3",
            "12345:abcdef",
            "track:67890"
        )] string? trackId)
    {
        Assert.That(Detector.IsLocal(trackId!), Is.False);
    }

    [Test]
    public void GetSourceId_ReturnsLocal_ForRootedPaths(
        [Values(
            @"C:\music\track.mp3",
            @"D:\songs\track.flac",
            @"\\server\share\track.mp3"
        )] string trackId)
    {
        Assert.That(Detector.GetSourceId(trackId), Is.EqualTo("local"));
    }

    [Test]
    public void GetSourceId_ReturnsYandex_ForNonLocalIds(
        [Values(
            null,
            "",
            "relative\\path\\track.mp3",
            "12345:abcdef",
            "track:67890"
        )] string? trackId)
    {
        Assert.That(Detector.GetSourceId(trackId!), Is.EqualTo("yandex"));
    }
}
