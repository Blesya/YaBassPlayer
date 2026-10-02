using Serilog;
using YamBassPlayer.Configuration;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

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
}