using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Services;

public interface IAuthService
{
    bool IsAuthorized { get; }
    AuthStorage Storage { get; }
    YandexMusicApi Api { get; }
    Task<bool> AuthorizeAsync(string token);
    Task<bool> AuthorizeFromConfigAsync();
}