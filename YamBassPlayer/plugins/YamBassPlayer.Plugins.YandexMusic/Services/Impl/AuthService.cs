using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Account;

namespace YamBassPlayer.Services.Impl;

public class AuthService : IAuthService
{
    private const string TokenKey = "token";
    private const string RefreshTokenKey = "refreshToken";

    private readonly IDeviceAuthorizationService _deviceAuthorization;
    private readonly IPluginSettings _settings;

    private AuthStorage _storage = new();
    private YandexMusicApi _api = new();
    private DeviceCodeChallenge? _deviceCode;

    public AuthService(IDeviceAuthorizationService deviceAuthorization, IPluginSettings settings)
    {
        _deviceAuthorization = deviceAuthorization;
        _settings = settings;
    }

    public bool IsAuthorized { get; private set; }

    public AuthStorage Storage => _storage;

    public YandexMusicApi Api => _api;

    public bool HasStoredToken => !string.IsNullOrWhiteSpace(_settings.Get(TokenKey));

    public bool HasStoredRefreshToken => !string.IsNullOrWhiteSpace(_settings.Get(RefreshTokenKey));

    public async Task<bool> AuthorizeAsync(string token)
    {

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            // Хранилище одно на всё время жизни плагина: источник, радио и избранное
            // держат ссылку на него. Подмена экземпляра оставила бы их без токена.
            await _api.User.AuthorizeAsync(_storage, token);
            IsAuthorized = _storage.IsAuthorized;

            if (IsAuthorized)
            {
                Log.Information("Авторизация в Яндекс.Музыке выполнена");
            }

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
        string? token = _settings.Get(TokenKey);
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return await AuthorizeAsync(token);
    }

    /// <inheritdoc/>
    public async Task<bool> TryRefreshAsync()
    {
        string? refreshToken = _settings.Get(RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;


        DeviceAuthResult result = await _deviceAuthorization.RefreshAsync(refreshToken);
        if (!result.IsAuthorized || !await AuthorizeAsync(result.AccessToken!))
        {
            return false;
        }

        _settings.Set(TokenKey, result.AccessToken);
        if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            _settings.Set(RefreshTokenKey, result.RefreshToken);

        Log.Information("Токен Яндекс.Музыки продлён по refresh-токену");
        return true;
    }

    /// <inheritdoc/>
    public async Task<string> StartQrAuthorizationAsync()
    {

        // Каждый вход по QR начинается с чистой сессии: старая CSRF/track_id уже недействительны.
        // Хранилище не подменяем — ссылку на него держат источник, радио и избранное.
        IsAuthorized = false;

        try
        {
            string link = await _api.User.GetAuthQRLinkAsync(_storage);
            return link;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось получить ссылку для входа по QR-коду");
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task<QrAuthState> CheckQrAuthorizationAsync()
    {
        if (_api is null || _storage is null)
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

        IsAuthorized = false;
        _deviceCode = await _deviceAuthorization.RequestCodeAsync();

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
        IsAuthorized = _storage.IsAuthorized;
        if (!IsAuthorized)
            return QrAuthState.Pending;

        Log.Information("Авторизация по QR-коду выполнена");
        return QrAuthState.Authorized;
    }
}
