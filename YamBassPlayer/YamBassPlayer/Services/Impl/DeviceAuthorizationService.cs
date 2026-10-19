using System.Text.Json;
using Serilog;
using YamBassPlayer.Configuration;
using YamBassPlayer.Constants;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// OAuth Device Flow Яндекс.Музыки. Схема: <c>POST device/code</c> выдаёт пару кодов,
/// пользователь вводит <c>user_code</c> на <c>ya.ru/device</c>, приложение опрашивает
/// <c>POST token</c> с <c>grant_type=device_code</c> и получает access- и refresh-токены.
/// </summary>
public sealed class DeviceAuthorizationService : IDeviceAuthorizationService
{
    private const int DefaultIntervalSeconds = 5;
    private const int DefaultLifetimeSeconds = 300;

    private readonly HttpClient _http;
    private readonly IAppConfiguration _configuration;

    public DeviceAuthorizationService(IAppConfiguration configuration)
        : this(configuration, new HttpClientHandler())
    {
    }

    /// <summary>Конструктор для тестов: подменяем транспорт и не ходим в сеть.</summary>
    public DeviceAuthorizationService(IAppConfiguration configuration, HttpMessageHandler handler)
    {
        _configuration = configuration;
        _http = new HttpClient(handler) { BaseAddress = new Uri(AuthConst.OAuthBaseUrl) };
    }

    /// <inheritdoc/>
    public async Task<DeviceCodeChallenge?> RequestCodeAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsync(
                "device/code",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = AuthConst.DeviceClientId,
                    ["device_id"] = _configuration.GetOrCreateDeviceId(),
                    ["device_name"] = AuthConst.DeviceName
                }),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("OAuth не выдал код устройства: {Status}", (int)response.StatusCode);
                return null;
            }

            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            JsonElement root = json.RootElement;

            string? deviceCode = ReadString(root, "device_code");
            string? userCode = ReadString(root, "user_code");
            if (string.IsNullOrWhiteSpace(deviceCode) || string.IsNullOrWhiteSpace(userCode))
                return null;

            string? verificationUrl = ReadString(root, "verification_url");

            return new DeviceCodeChallenge(
                deviceCode,
                userCode,
                string.IsNullOrWhiteSpace(verificationUrl) ? AuthConst.DeviceVerificationUrl : verificationUrl,
                TimeSpan.FromSeconds(ReadInt(root, "interval", DefaultIntervalSeconds)),
                TimeSpan.FromSeconds(ReadInt(root, "expires_in", DefaultLifetimeSeconds)));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось запросить код устройства у OAuth Яндекса");
            return null;
        }
    }

    /// <inheritdoc/>
    public Task<DeviceAuthResult> PollAsync(string deviceCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
            return Task.FromResult(Failed());

        return RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "device_code",
            ["code"] = deviceCode,
            ["client_id"] = AuthConst.DeviceClientId,
            ["client_secret"] = AuthConst.DeviceClientSecret
        }, ct);
    }

    /// <inheritdoc/>
    public Task<DeviceAuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Task.FromResult(Failed());

        return RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = AuthConst.DeviceClientId,
            ["client_secret"] = AuthConst.DeviceClientSecret
        }, ct);
    }

    private async Task<DeviceAuthResult> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsync("token", new FormUrlEncodedContent(form), ct);
            string body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                string error = ReadError(body);
                DeviceAuthState state = error switch
                {
                    // slow_down — «слишком часто опрашиваете»: ждём дальше, интервал задаёт вызывающий.
                    "authorization_pending" or "slow_down" => DeviceAuthState.Pending,
                    "expired_token" => DeviceAuthState.Expired,
                    "access_denied" => DeviceAuthState.Denied,
                    _ => DeviceAuthState.Error
                };

                if (state == DeviceAuthState.Error)
                    Log.Warning("OAuth отклонил запрос токена: {Error}", error);

                return new DeviceAuthResult(state, null, null, null);
            }

            using JsonDocument json = JsonDocument.Parse(body);
            JsonElement root = json.RootElement;

            string? accessToken = ReadString(root, "access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
                return Failed();

            int expiresIn = ReadInt(root, "expires_in", 0);

            return new DeviceAuthResult(
                DeviceAuthState.Authorized,
                accessToken,
                ReadString(root, "refresh_token"),
                expiresIn > 0 ? TimeSpan.FromSeconds(expiresIn) : null);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось получить токен по Device Flow");
            return Failed();
        }
    }

    private static DeviceAuthResult Failed() => new(DeviceAuthState.Error, null, null, null);

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;

    private static int ReadInt(JsonElement root, string name, int fallback) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result)
            ? result
            : fallback;

    private static string ReadError(string body)
    {
        try
        {
            using JsonDocument json = JsonDocument.Parse(body);
            return ReadString(json.RootElement, "error") ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
