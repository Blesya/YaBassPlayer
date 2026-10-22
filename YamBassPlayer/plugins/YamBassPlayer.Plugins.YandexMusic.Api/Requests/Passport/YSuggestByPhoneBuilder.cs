using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Passport;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Passport
{
    [YPassportRequest(WebRequestMethods.Http.Post, "pwl-yandex/api/passport/suggest/by_phone")]
    public class YSuggestByPhoneBuilder : YPassportRequestBuilder<YSuggestByPhoneResult, string>
    {
        public YSuggestByPhoneBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
        
        protected override HttpContent GetContent(string tuple)
        {
            return GetJsonContent(new {
                track_id = storage.AuthToken.TrackId,
                can_use_anmon = true
            });
        }
    }
}