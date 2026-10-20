using System.Security.Authentication;
using System.Threading.Tasks;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Common.Exceptions;
using YamBassPlayer.Api.Models.Account;
using YamBassPlayer.Api.Requests.MobileProxy;

namespace YamBassPlayer.Api.API
{
    public partial class YMobileProxyAPI : YCommonAPI
    {
        public YMobileProxyAPI(YandexMusicApi yandex) : base(yandex)
        {
        }

        public async Task<YAccessToken> GetTokenBySessionIdAsync(AuthStorage storage)
        {
            YAccessToken accessToken = await new YGetTokenBySessionIdBuilder(api, storage)
                .Build(null)
                .GetResponseAsync();

            if (accessToken.Status == YAuthStatus.Error || accessToken.Errors is not null)
                throw new YApiException($"Ошибки получения токена: {string.Join(", ", accessToken.Errors)}");

            storage.Token = accessToken.AccessToken;

            return accessToken;
        }

        public async Task<YAccessToken> GetXTokenAsync(AuthStorage storage, YAccessToken token)
        {
            if (string.IsNullOrEmpty(storage.Token))
                throw new AuthenticationException($"Невозможно получить код доступа. Выполните процесс логина {nameof(GetTokenBySessionIdAsync)}");

            YAccessToken accessToken = await new YGetAccessTokenBuilder(api, storage)
                .Build(token)
                .GetResponseAsync();

            storage.Token = accessToken.AccessToken;

            return accessToken;
        }
    }
}