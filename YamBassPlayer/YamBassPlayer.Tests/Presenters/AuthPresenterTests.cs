using Moq;
using YamBassPlayer.Configuration;
using YamBassPlayer.Enums;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class AuthPresenterTests
{
    private static readonly TimeSpan FastPoll = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan LongLifetime = TimeSpan.FromMinutes(1);

    private Mock<IAuthService> _authService = null!;
    private Mock<IQrCodeRenderer> _qrRenderer = null!;
    private Mock<IAuthView> _view = null!;
    private Mock<IErrorHandler> _errorHandler = null!;
    private Mock<IAppConfiguration> _configuration = null!;
    private FakeUiDispatcher _ui = null!;
    private TaskCompletionSource<string> _pendingQrLink = null!;
    private AuthPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _authService = new Mock<IAuthService>();
        _qrRenderer = new Mock<IQrCodeRenderer>();
        _view = new Mock<IAuthView>();
        _errorHandler = new Mock<IErrorHandler>();
        _configuration = new Mock<IAppConfiguration>();
        _ui = new FakeUiDispatcher();

        _authService.SetupGet(s => s.Storage).Returns(new AuthStorage());

        // По умолчанию получение QR-ссылки «зависает», чтобы тесты не запускали опрос.
        _pendingQrLink = new TaskCompletionSource<string>();
        _authService.Setup(s => s.StartQrAuthorizationAsync()).Returns(_pendingQrLink.Task);

        _presenter = new AuthPresenter(
            _authService.Object,
            _qrRenderer.Object,
            _view.Object,
            _ui,
            _errorHandler.Object,
            _configuration.Object,
            pollInterval: FastPoll,
            qrLifetime: LongLifetime);
    }

    // ──────────────── Токен ────────────────

    [Test]
    public async Task SubmitTokenAsync_Success_SavesTokenAndCloses()
    {
        _authService.Setup(s => s.AuthorizeAsync("good")).ReturnsAsync(true);

        await _presenter.SubmitTokenAsync("good");

        Assert.That(_presenter.Authorized, Is.True);
        _configuration.Verify(c => c.SaveToken("good"), Times.Once);
        _view.Verify(v => v.Close(), Times.Once);
    }

    [Test]
    public async Task SubmitTokenAsync_Failure_ShowsErrorAndKeepsWindowOpen()
    {
        _authService.Setup(s => s.AuthorizeAsync("bad")).ReturnsAsync(false);

        await _presenter.SubmitTokenAsync("bad");

        Assert.That(_presenter.Authorized, Is.False);
        _configuration.Verify(c => c.SaveToken(It.IsAny<string>()), Times.Never);
        _view.Verify(v => v.Close(), Times.Never);
        _view.Verify(v => v.SetStatus(It.Is<string>(s => s.Contains("не подошёл")), true), Times.Once);
    }

    [Test]
    public async Task SubmitTokenAsync_Exception_ReportsErrorAndKeepsWindowOpen()
    {
        _authService.Setup(s => s.AuthorizeAsync("boom"))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _presenter.SubmitTokenAsync("boom");

        Assert.That(_presenter.Authorized, Is.False);
        _errorHandler.Verify(e => e.Handle(It.IsAny<Exception>()), Times.Once);
        _view.Verify(v => v.Close(), Times.Never);
    }

    // ──────────────── QR-код ────────────────

    [Test]
    public async Task StartQrFlowAsync_WhenConfirmed_ClosesWindow()
    {
        _authService.Setup(s => s.StartQrAuthorizationAsync()).ReturnsAsync("https://link");
        _qrRenderer.Setup(r => r.Render("https://link")).Returns("QR");
        _authService.Setup(s => s.CheckQrAuthorizationAsync()).ReturnsAsync(QrAuthState.Authorized);

        await _presenter.StartQrFlowAsync();

        Assert.That(_presenter.Authorized, Is.True);
        _view.Verify(v => v.ShowQrCode("QR", "https://link"), Times.Once);
        _view.Verify(v => v.Close(), Times.Once);
    }

    [Test]
    public async Task StartQrFlowAsync_NoLink_FallsBackToToken()
    {
        _authService.Setup(s => s.StartQrAuthorizationAsync()).ReturnsAsync(string.Empty);

        await _presenter.StartQrFlowAsync();

        Assert.That(_presenter.Authorized, Is.False);
        _view.Verify(v => v.ShowQrCode(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _view.Verify(v => v.SelectMethod(AuthMethod.Token), Times.Once);
        _view.Verify(v => v.SetStatus(It.Is<string>(s => s.Contains("QR-вход")), true), Times.Once);
    }

    [Test]
    public async Task StartQrFlowAsync_PendingThenAuthorized_ClosesOnConfirmation()
    {
        _authService.Setup(s => s.StartQrAuthorizationAsync()).ReturnsAsync("https://link");
        _qrRenderer.Setup(r => r.Render(It.IsAny<string>())).Returns("QR");
        _authService.SetupSequence(s => s.CheckQrAuthorizationAsync())
            .ReturnsAsync(QrAuthState.Pending)
            .ReturnsAsync(QrAuthState.Pending)
            .ReturnsAsync(QrAuthState.Authorized);

        await _presenter.StartQrFlowAsync();

        Assert.That(_presenter.Authorized, Is.True);
        _view.Verify(v => v.Close(), Times.Once);
    }

    // ──────────────── Привязка событий ────────────────

    [Test]
    public void Attach_OffersQrAndTokenMethods()
    {
        _presenter.Attach();

        _view.Verify(v => v.SetMethods(It.Is<IReadOnlyList<AuthMethod>>(
            m => m.Contains(AuthMethod.Qr) && m.Contains(AuthMethod.Token))), Times.Once);

        _presenter.Detach();
    }

    [Test]
    public void Cancel_ClosesWindow()
    {
        _presenter.Attach();

        _view.Raise(v => v.OnCancelRequested += null);

        _view.Verify(v => v.Close(), Times.Once);

        _presenter.Detach();
    }

    [Test]
    public void SelectToken_StopsQrPolling()
    {
        _presenter.Attach();

        _view.Raise(v => v.OnMethodSelected += null, AuthMethod.Token);

        _view.Verify(v => v.SetStatus(It.Is<string>(s => s.Contains("токен")), false), Times.AtLeastOnce);

        _presenter.Detach();
    }

    [Test]
    public void RefreshQr_RequestsNewCode()
    {
        _presenter.Attach();
        _authService.Invocations.Clear();

        _view.Raise(v => v.OnQrRefreshRequested += null);

        _authService.Verify(s => s.StartQrAuthorizationAsync(), Times.Once);

        _presenter.Detach();
    }
}
