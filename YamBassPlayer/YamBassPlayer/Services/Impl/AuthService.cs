using Serilog;
using YamBassPlayer.Configuration;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Account;

namespace YamBassPlayer.Services.Impl;

public class AuthService : IAuthService
{
    private readonly IDeviceAuthorizationService _deviceAuthorization;
    private readonly IAppConfiguration _configuration;

    private AuthStorage? _storage;
    private YandexMusicApi? _api;
    private DeviceCodeChallenge? _deviceCode;

    public AuthService(IDeviceAuthorizationService deviceAuthorization, IAppConfiguration configuration)
    {
        _deviceAuthorization = deviceAuthorization;
        _configuration = configuration;
    }

    public bool IsAuthorized { get; private set; }
        
    public AuthStorage Storage => _storage 
                                  ?? throw new InvalidOperationException("AuthService not initialized. Call AuthorizeAsync first.");
        
    public YandexMusicApi Api => _api 
                                 ?? throw new InvalidOperationException("AuthService not initialized. Call AuthorizeAsync first.");

    public static bool HasToken()
    {
        string? token = AppConfiguration.GetYandexMusicToken();
        return !string.IsNullOrWhiteSpace(token);
    }

    public static bool HasRefreshToken()
    {
        string? refreshToken = AppConfiguration.GetRefreshToken();
        return !string.IsNullOrWhiteSpace(refreshToken);
    }

    public async Task<bool> AuthorizeAsync(string token)
    {
        Logging.LogBeforeCall();

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            _storage = new AuthStorage();
            _api = new YandexMusicApi();
            await _api.User.AuthorizeAsync(_storage, token);
            IsAuthorized = _storage.IsAuthorized;

            if (IsAuthorized)
            {
                Log.Information("Авторизация в Яндекс.Музыке выполнена");
            }

            Logging.LogAfterCall();
            return IsAuthorized;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось авторизоваться в Яндекс.Музыке");
            IsAuthorized = false;
            return false;
        }
    }

    public async Task<bool> AuthorizeFromConfigAsync()
    {
        string? token = _configuration.GetYandexMusicToken();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return await AuthorizeAsync(token);
    }

    /// <inheritdoc/>
    public async Task<bool> TryRefreshAsync()
    {
        string? refreshToken = _configuration.GetRefreshToken();
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        Logging.LogBeforeCall();

        DeviceAuthResult result = await _deviceAuthorization.RefreshAsync(refreshToken);
        if (!result.IsAuthorized || !await AuthorizeAsync(result.AccessToken!))
        {
            Logging.LogAfterCall();
            return false;
        }

        _configuration.SaveToken(result.AccessToken!);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            _configuration.SaveRefreshToken(result.RefreshToken);

        Log.Information("Токен Яндекс.Музыки продлён по refresh-токену");
        Logging.LogAfterCall();
        return true;
    }

    /// <inheritdoc/>
    public async Task<string> StartQrAuthorizationAsync()
    {
        Logging.LogBeforeCall();

        // Каждый вход по QR начинается с чистой сессии: старая CSRF/track_id уже недействительны.
        _storage = new AuthStorage();
        _api = new YandexMusicApi();
        IsAuthorized = false;

        try
        {
            string link = await _api.User.GetAuthQRLinkAsync(_storage);
            Logging.LogAfterCall();
            return link;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось получить ссылку для входа по QR-коду");
            Logging.LogAfterCall();
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task<QrAuthState> CheckQrAuthorizationAsync()
    {
        if (_api == null || _storage == null)
            return QrAuthState.Error;

        try
        {
            YAuthQRStatus status = await _api.User.AuthorizeByQRAsync(_storage);

            // Пока пользователь не подтвердил вход, библиотека возвращает не-OK статус.
            if (status.Status != YAuthStatus.Ok)
                return QrAuthState.Pending;

            return CompleteQrAuthorization();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка проверки входа по QR-коду");

            // Библиотека бросает исключение и в момент, когда токен уже получен,
            // поэтому перед отказом ещё раз смотрим на состояние сессии.
            if (_storage.IsAuthorized && !string.IsNullOrWhiteSpace(_storage.Token))
                return CompleteQrAuthorization();

            return QrAuthState.Error;
        }
    }

    /// <inheritdoc/>
    public async Task<DeviceCodeChallenge?> StartDeviceAuthorizationAsync()
    {
        Logging.LogBeforeCall();

        IsAuthorized = false;
        _deviceCode = await _deviceAuthorization.RequestCodeAsync();

        Logging.LogAfterCall();
        return _deviceCode;
    }

    /// <inheritdoc/>
    public async Task<DeviceAuthResult> CheckDeviceAuthorizationAsync()
    {
        if (_deviceCode == null)
            return new DeviceAuthResult(DeviceAuthState.Error, null, null, null);

        DeviceAuthResult result = await _deviceAuthorization.PollAsync(_deviceCode.DeviceCode);
        if (!result.IsAuthorized)
            return result;

        // OAuth токен выдал, но проверяем его на API Музыки: только после этого
        // сервис считается авторизованным.
        if (!await AuthorizeAsync(result.AccessToken!))
            return new DeviceAuthResult(DeviceAuthState.Error, null, null, null);

        Log.Information("Авторизация по коду устройства выполнена");
        return result;
    }

    private QrAuthState CompleteQrAuthorization()
    {
        IsAuthorized = _storage!.IsAuthorized;
        if (!IsAuthorized)
            return QrAuthState.Pending;

        Log.Information("Авторизация по QR-коду выполнена");
        return QrAuthState.Authorized;
    }
}
