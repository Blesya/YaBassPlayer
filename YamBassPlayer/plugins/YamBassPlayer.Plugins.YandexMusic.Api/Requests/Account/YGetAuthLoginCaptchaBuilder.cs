using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Account;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Account
{
    [YPassportRequest(WebRequestMethods.Http.Post, "registration-validations/checkHuman")]
    internal class YGetAuthLoginCaptchaBuilder : YRequestBuilder<YAuthBase, string>
    {
        public YGetAuthLoginCaptchaBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(string tuple)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "csrf_token", storage.AuthToken.CsfrToken },
                { "track_id", storage.AuthToken.TrackId },
                { "answer", tuple }
            });
        }

        protected override void SetCustomHeaders(HttpRequestHeaders headers)
        {
            headers.Add("X-Requested-With", "XMLHttpRequest");
        }
    }
}