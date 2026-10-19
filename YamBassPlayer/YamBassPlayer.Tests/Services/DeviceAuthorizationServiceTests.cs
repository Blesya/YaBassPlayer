using System.Net;
using System.Text;
using Moq;
using YamBassPlayer.Configuration;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class DeviceAuthorizationServiceTests
{
    [Test]
    public async Task RequestCodeAsync_ReturnsChallenge()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """
            {"device_code":"dev","user_code":"bc7zfzxu","verification_url":"https://ya.ru/device","interval":5,"expires_in":300}
            """));

        DeviceCodeChallenge? challenge = await CreateService(handler).RequestCodeAsync();

        Assert.That(challenge, Is.Not.Null);
        Assert.That(challenge!.DeviceCode, Is.EqualTo("dev"));
        Assert.That(challenge.UserCode, Is.EqualTo("bc7zfzxu"));
        Assert.That(challenge.VerificationUrl, Is.EqualTo("https://ya.ru/device"));
        Assert.That(challenge.Interval, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That(challenge.Lifetime, Is.EqualTo(TimeSpan.FromSeconds(300)));

        Assert.That(handler.Paths.Single(), Is.EqualTo("/device/code"));
        Assert.That(handler.Bodies.Single(), Does.Contain("client_id=23cabbbdc6cd418abb4b39c32c41195d"));
        Assert.That(handler.Bodies.Single(), Does.Contain("device_id=device-1"));
    }

    [Test]
    public async Task RequestCodeAsync_HttpError_ReturnsNull()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_client"}"""));

        Assert.That(await CreateService(handler).RequestCodeAsync(), Is.Null);
    }

    [Test]
    public async Task PollAsync_AuthorizationPending_ReturnsPending()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}"""));

        DeviceAuthResult result = await CreateService(handler).PollAsync("dev");

        Assert.That(result.State, Is.EqualTo(DeviceAuthState.Pending));
        Assert.That(handler.Bodies.Single(), Does.Contain("grant_type=device_code"));
        Assert.That(handler.Bodies.Single(), Does.Contain("code=dev"));
    }

    [Test]
    public async Task PollAsync_SlowDown_ReturnsPending()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"slow_down"}"""));

        Assert.That((await CreateService(handler).PollAsync("dev")).State, Is.EqualTo(DeviceAuthState.Pending));
    }

    [Test]
    public async Task PollAsync_ExpiredToken_ReturnsExpired()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"expired_token"}"""));

        Assert.That((await CreateService(handler).PollAsync("dev")).State, Is.EqualTo(DeviceAuthState.Expired));
    }

    [Test]
    public async Task PollAsync_AccessDenied_ReturnsDenied()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"access_denied"}"""));

        Assert.That((await CreateService(handler).PollAsync("dev")).State, Is.EqualTo(DeviceAuthState.Denied));
    }

    [Test]
    public async Task PollAsync_UnknownError_ReturnsError()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        Assert.That((await CreateService(handler).PollAsync("dev")).State, Is.EqualTo(DeviceAuthState.Error));
    }

    [Test]
    public async Task PollAsync_Authorized_ReturnsTokens()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """
            {"access_token":"access","refresh_token":"refresh","expires_in":31536000,"token_type":"bearer"}
            """));

        DeviceAuthResult result = await CreateService(handler).PollAsync("dev");

        Assert.That(result.State, Is.EqualTo(DeviceAuthState.Authorized));
        Assert.That(result.AccessToken, Is.EqualTo("access"));
        Assert.That(result.RefreshToken, Is.EqualTo("refresh"));
        Assert.That(result.Lifetime, Is.EqualTo(TimeSpan.FromSeconds(31536000)));
    }

    [Test]
    public async Task PollAsync_EmptyCode_DoesNotCallOAuth()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{}"));

        DeviceAuthResult result = await CreateService(handler).PollAsync(string.Empty);

        Assert.That(result.State, Is.EqualTo(DeviceAuthState.Error));
        Assert.That(handler.Paths, Is.Empty);
    }

    [Test]
    public async Task RefreshAsync_SendsRefreshGrant()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """
            {"access_token":"new-access","refresh_token":"new-refresh","expires_in":31536000}
            """));

        DeviceAuthResult result = await CreateService(handler).RefreshAsync("old-refresh");

        Assert.That(result.State, Is.EqualTo(DeviceAuthState.Authorized));
        Assert.That(result.AccessToken, Is.EqualTo("new-access"));
        Assert.That(result.RefreshToken, Is.EqualTo("new-refresh"));
        Assert.That(handler.Paths.Single(), Is.EqualTo("/token"));
        Assert.That(handler.Bodies.Single(), Does.Contain("grant_type=refresh_token"));
        Assert.That(handler.Bodies.Single(), Does.Contain("refresh_token=old-refresh"));
    }

    [Test]
    public async Task RefreshAsync_Rejected_ReturnsError()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        Assert.That((await CreateService(handler).RefreshAsync("stale")).State, Is.EqualTo(DeviceAuthState.Error));
    }

    private static DeviceAuthorizationService CreateService(HttpMessageHandler handler)
    {
        var configuration = new Mock<IAppConfiguration>();
        configuration.Setup(c => c.GetOrCreateDeviceId()).Returns("device-1");
        return new DeviceAuthorizationService(configuration.Object, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Транспорт-заглушка: запоминает запросы и отвечает заданным телом.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public List<string> Paths { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Bodies.Add(request.Content is null
                ? string.Empty
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());

            return Task.FromResult(_respond(request));
        }
    }
}
