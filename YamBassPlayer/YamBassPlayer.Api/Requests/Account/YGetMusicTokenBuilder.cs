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
    [YOAuthMobile(WebRequestMethods.Http.Post, "/1/token")]
    internal class YGetMusicTokenBuilder : YRequestBuilder<YAccessToken, string>
    {
        public YGetMusicTokenBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(string tuple)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", YConstants.ClientId },
                { "client_secret", YConstants.ClientSecret },
                { "grant_type", "x-token" },
                { "access_token", storage.AccessToken.AccessToken }
            });
        }

        protected override void SetCustomHeaders(HttpRequestHeaders headers)
        {
            headers.Remove("Authorization");

            base.SetCustomHeaders(headers);
        }
    }
}