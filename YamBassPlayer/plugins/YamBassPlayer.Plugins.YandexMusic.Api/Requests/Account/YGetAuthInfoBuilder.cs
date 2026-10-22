using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Account;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Account
{
    [YApiRequest(WebRequestMethods.Http.Get, "account/status")]
    public class YGetAuthInfoBuilder: YRequestBuilder<YResponse<YAccountResult>, object>
    {
        public YGetAuthInfoBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}