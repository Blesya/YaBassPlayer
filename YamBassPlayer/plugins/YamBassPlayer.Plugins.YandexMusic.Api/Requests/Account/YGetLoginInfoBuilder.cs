using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Account;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Account
{
    [YLoginRequest(WebRequestMethods.Http.Get, "info")]
    public class YGetLoginInfoBuilder : YRequestBuilder<YLoginInfo, object>
    {
        public YGetLoginInfoBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}