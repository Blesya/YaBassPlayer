using Serilog;
using YamBassPlayer.Configuration;
using YamBassPlayer.Enums;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Account;

namespace YamBassPlayer.Services.Impl;

public class AuthService : IAuthService
{
    private AuthStorage? _storage;
    private YandexMusicApi? _api;

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
        string? token = AppConfiguration.GetYandexMusicToken();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return await AuthorizeAsync(token);
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

    private QrAuthState CompleteQrAuthorization()
    {
        IsAuthorized = _storage!.IsAuthorized;
        if (!IsAuthorized)
            return QrAuthState.Pending;

        Log.Information("Авторизация по QR-коду выполнена");
        return QrAuthState.Authorized;
    }
}
