using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Account;

namespace YamBassPlayer.Api.API
{
    public partial class YMobileProxyAPI
    {
        public YAccessToken GetTokenBySessionId(AuthStorage storage)
        {
            return GetTokenBySessionIdAsync(storage).GetAwaiter().GetResult();
        }

        public YAccessToken GetTokenByAccessToken(AuthStorage storage, YAccessToken token)
        {
            return GetXTokenAsync(storage, token).GetAwaiter().GetResult();
        }
    }
}