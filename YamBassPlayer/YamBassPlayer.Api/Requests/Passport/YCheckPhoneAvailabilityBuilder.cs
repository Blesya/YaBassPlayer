using System.Collections.Generic;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Passport;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Passport
{
    [YPassportRequest(WebRequestMethods.Http.Post, "pwl-yandex/api/passport/suggest/check_availability")]
    public class YCheckPhoneAvailabilityBuilder : YPassportRequestBuilder<YCheckAvailabilityResult, string>
    {
        public YCheckPhoneAvailabilityBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
        
        protected override HttpContent GetContent(string tuple)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "track_id", storage.AuthToken.TrackId },
                { "phone_number", tuple },
                { "can_use_anmon", "true" },
                { "check_for_push", "true" },
                { "push_suggest_log_all_subscriptions", "false" }
            });
        }
    }
}