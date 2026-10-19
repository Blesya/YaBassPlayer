using Moq;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class AudioPlayerServiceTests
{
    [Test]
    public void SetVolumePercent_ClampsToRange()
    {
        var service = new AudioPlayerService(Mock.Of<IBassEqualizer>());

        service.SetVolumePercent(150);
        Assert.That(service.VolumePercent, Is.EqualTo(100));

        service.SetVolumePercent(-10);
        Assert.That(service.VolumePercent, Is.Zero);
    }

    [Test]
    public void SetVolumePercent_WithoutStream_DoesNotThrow()
    {
        var service = new AudioPlayerService(Mock.Of<IBassEqualizer>());

        Assert.DoesNotThrow(() => service.SetVolumePercent(42));
        Assert.That(service.VolumePercent, Is.EqualTo(42));
    }

    [Test]
    public void ResumeAndStop_WithoutTrack_DoNotThrow()
    {
        var service = new AudioPlayerService(Mock.Of<IBassEqualizer>());

        Assert.DoesNotThrow(() => service.Resume());
        Assert.DoesNotThrow(() => service.Stop());
    }
}
