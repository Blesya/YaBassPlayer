using Moq;
using YamBassPlayer.Configuration;
using YamBassPlayer.Constants;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Tests.Views;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class UpdatePresenterTests : ViewTestBase
{
    private Mock<IAudioPlayer> _audioPlayer = null!;
    private Mock<IAppConfiguration> _configuration = null!;
    private FakeDialogView _dialogView = null!;
    private FakeInstaller _installer = null!;
    private Mock<IUpdateService> _updateService = null!;

    [SetUp]
    public void SetUp()
    {
        _audioPlayer = new Mock<IAudioPlayer>();
        _configuration = new Mock<IAppConfiguration>();
        _configuration.Setup(c => c.GetUpdatesEnabled()).Returns(true);
        _dialogView = new FakeDialogView();
        _installer = new FakeInstaller();
        _updateService = new Mock<IUpdateService>();
    }

    [Test]
    public async Task CheckAsync_NewerVersion_ShowsDialog()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUpdate(NextVersion()));

        await CreatePresenter().CheckAsync(manual: false);

        Assert.That(_dialogView.ShowCount, Is.EqualTo(1));
        Assert.That(_dialogView.ShownVersion, Is.EqualTo(NextVersion()));
    }

    [Test]
    public async Task CheckAsync_SameVersion_Manual_ShowsUpToDate()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUpdate(CurrentVersion()));

        await CreatePresenter().CheckAsync(manual: true);

        Assert.That(_dialogView.ShowCount, Is.Zero);
        Assert.That(_dialogView.UpToDateVersion, Is.EqualTo(AppInfo.Version));
    }

    [Test]
    public async Task CheckAsync_SameVersion_Silent_ShowsNothing()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUpdate(CurrentVersion()));

        await CreatePresenter().CheckAsync(manual: false);

        Assert.That(_dialogView.ShowCount, Is.Zero);
        Assert.That(_dialogView.UpToDateVersion, Is.Null);
    }

    [Test]
    public async Task CheckAsync_NoRelease_Manual_ShowsUpToDate()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateInfo?)null);

        await CreatePresenter().CheckAsync(manual: true);

        Assert.That(_dialogView.UpToDateVersion, Is.EqualTo(AppInfo.Version));
    }

    [Test]
    public async Task CheckAsync_PostponedVersion_IsNotShownAgain()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUpdate(NextVersion()));
        _dialogView.Result = new UpdateDialogResult(UpdateDecision.Postpone, null);

        var presenter = CreatePresenter();
        await presenter.CheckAsync(manual: false);
        await presenter.CheckAsync(manual: false);

        Assert.That(_dialogView.ShowCount, Is.EqualTo(1));
        Assert.That(_installer.ScheduledZipPath, Is.Null);
    }

    [Test]
    public async Task CheckAsync_Install_SchedulesUpdateAndStopsPlayback()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUpdate(NextVersion()));
        _dialogView.Result = new UpdateDialogResult(UpdateDecision.Install, @"C:\temp\update.zip");

        await CreatePresenter().CheckAsync(manual: true);

        Assert.That(_installer.ScheduledZipPath, Is.EqualTo(@"C:\temp\update.zip"));
        Assert.That(_installer.ScheduledVersion, Is.EqualTo(NextVersion().ToString()));
        _audioPlayer.Verify(p => p.Free(), Times.Once);
    }

    [Test]
    public async Task CheckAsync_ServiceFails_Manual_ShowsError()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("нет сети"));

        await CreatePresenter().CheckAsync(manual: true);

        Assert.That(_dialogView.ErrorMessage, Does.Contain("нет сети"));
    }

    [Test]
    public async Task CheckAsync_ServiceFails_Silent_DoesNotShowError()
    {
        _updateService.Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("нет сети"));

        await CreatePresenter().CheckAsync(manual: false);

        Assert.That(_dialogView.ErrorMessage, Is.Null);
    }

    private UpdatePresenter CreatePresenter() => new(
        _updateService.Object,
        _dialogView,
        _installer,
        _audioPlayer.Object,
        new ImmediateDispatcher(),
        _configuration.Object);

    private static Version CurrentVersion() => AppInfo.ParsedVersion!;

    private static Version NextVersion()
    {
        Version current = CurrentVersion();
        return new Version(current.Major, current.Minor, current.Build + 1);
    }

    private static UpdateInfo CreateUpdate(Version version) => new(
        version,
        $"v{version}",
        $"YamBassPlayer-{version}-win-x64.zip",
        "https://example.org/app.zip",
        0,
        null,
        null);

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();

        public void StartTimer(TimeSpan interval, Func<bool> onTick) { }
    }

    private sealed class FakeDialogView : IUpdateDialogView
    {
        public string? ErrorMessage { get; private set; }
        public UpdateDialogResult Result { get; set; } = new(UpdateDecision.Postpone, null);
        public int ShowCount { get; private set; }
        public Version? ShownVersion { get; private set; }
        public string? UpToDateVersion { get; private set; }

        public UpdateDialogResult Show(
            UpdateInfo info,
            string currentVersion,
            Func<IProgress<int>, CancellationToken, Task<string>> download)
        {
            ShowCount++;
            ShownVersion = info.Version;
            return Result;
        }

        public void ShowUpToDate(string currentVersion) => UpToDateVersion = currentVersion;

        public void ShowError(string message) => ErrorMessage = message;
    }

    private sealed class FakeInstaller : IUpdateInstaller
    {
        public string? ScheduledVersion { get; private set; }
        public string? ScheduledZipPath { get; private set; }

        public void Schedule(string zipPath, string version)
        {
            ScheduledZipPath = zipPath;
            ScheduledVersion = version;
        }

        public void LaunchPending() { }
    }
}
