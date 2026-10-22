using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Account;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.MobileProxy
{
    [YMobileProxyRequest(WebRequestMethods.Http.Get, "1/bundle/account/short_info/")]
    internal class YGetShortAccountInifoBuilder : YRequestBuilder<YShortAccountInfo, object>
    {
        public YGetShortAccountInifoBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override NameValueCollection GetQueryParams(object tuple)
        {
            return new NameValueCollection {
                { "avatar_size", "islands-300" }
            };
        }

        protected override void SetCustomHeaders(HttpRequestHeaders headers)
        {
            headers.Add("Authorization", $"OAuth {storage.AccessToken.AccessToken}");
        }
    }
}