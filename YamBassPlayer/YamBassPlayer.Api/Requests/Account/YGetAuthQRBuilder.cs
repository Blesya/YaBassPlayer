using System.Collections.Generic;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Account;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Account
{
    [YPassportRequest(WebRequestMethods.Http.Post, "registration-validations/auth/password/submit")]
    internal class YGetAuthQRBuilder : YRequestBuilder<YAuthQR, string>
    {
        public YGetAuthQRBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(string tuple)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                    { "csrf_token", storage.AuthToken.CsfrToken },
                    { "retpath", "https://passport.yandex.ru/profile" },
                    { "with_code", "1" },
                });
        }
    }
}