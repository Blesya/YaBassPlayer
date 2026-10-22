using System.Collections.Specialized;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Account
{
    [YPassportRequest(WebRequestMethods.Http.Get, "am")]
    internal class YGetAuthMethodsBuilder : YRequestBuilder<HttpResponseMessage, string>
    {
        public YGetAuthMethodsBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override NameValueCollection GetQueryParams(string tuple)
        {
            return new NameValueCollection {
                { "app_platform", "android" }
            };
        }
    }
}