using YamBassPlayer.Constants;

namespace YamBassPlayer.Tests.Constants;

[TestFixture]
public sealed class AppInfoTests
{
    [Test]
    public void WindowTitle_ContainsAppNameAndVersion()
    {
        Assert.That(AppInfo.WindowTitle, Is.EqualTo($"{AppInfo.AppName} {AppInfo.Version}"));
    }

    [Test]
    public void Version_HasNoBuildCommitSuffix()
    {
        Assert.That(AppInfo.Version, Does.Not.Contain("+"));
    }
}
