using YamBassPlayer.Updater;

namespace YamBassPlayer.Tests.Updater;

[TestFixture]
public sealed class UpdaterArgumentsTests
{
    [Test]
    public void TryParse_FullSet_ReturnsArguments()
    {
        string[] args =
        [
            "--pid", "4242",
            "--zip", @"C:\temp\update.zip",
            "--target", @"C:\apps\YamBassPlayer",
            "--restart", "YamBassPlayer.exe",
            "--log", @"C:\temp\updater.log"
        ];

        bool parsed = UpdaterArguments.TryParse(args, out UpdaterArguments? result, out string? error);

        Assert.That(parsed, Is.True, error);
        Assert.That(result!.ParentProcessId, Is.EqualTo(4242));
        Assert.That(result.ZipPath, Is.EqualTo(@"C:\temp\update.zip"));
        Assert.That(result.TargetDirectory, Is.EqualTo(@"C:\apps\YamBassPlayer"));
        Assert.That(result.RestartFileName, Is.EqualTo("YamBassPlayer.exe"));
        Assert.That(result.LogPath, Is.EqualTo(@"C:\temp\updater.log"));
    }

    [Test]
    public void TryParse_WithoutOptionalArguments_UsesDefaults()
    {
        string[] args = ["--pid", "1", "--zip", "update.zip", "--target", @"C:\apps"];

        bool parsed = UpdaterArguments.TryParse(args, out UpdaterArguments? result, out _);

        Assert.That(parsed, Is.True);
        Assert.That(result!.RestartFileName, Is.EqualTo(UpdaterArguments.DefaultRestartFileName));
        Assert.That(result.LogPath, Is.Null);
    }

    [TestCase("--pid", "abc", "--zip", "u.zip", "--target", @"C:\apps")]
    [TestCase("--pid", "0", "--zip", "u.zip", "--target", @"C:\apps")]
    [TestCase("--zip", "u.zip", "--target", @"C:\apps")]
    [TestCase("--pid", "1", "--target", @"C:\apps")]
    [TestCase("--pid", "1", "--zip", "u.zip")]
    public void TryParse_InvalidArguments_ReturnsFalse(params string[] args)
    {
        bool parsed = UpdaterArguments.TryParse(args, out UpdaterArguments? result, out string? error);

        Assert.That(parsed, Is.False);
        Assert.That(result, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void TryParse_ValueWithoutArgument_ReturnsFalse()
    {
        bool parsed = UpdaterArguments.TryParse(["--pid"], out _, out string? error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Does.Contain("--pid"));
    }

    [Test]
    public void TryParse_UnknownArgument_ReturnsFalse()
    {
        bool parsed = UpdaterArguments.TryParse(["--pid", "1", "--nope", "x"], out _, out string? error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Does.Contain("--nope"));
    }
}
