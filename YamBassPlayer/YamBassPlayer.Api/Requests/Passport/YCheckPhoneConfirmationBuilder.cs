using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Passport;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Passport
{
    [YPassportRequest(WebRequestMethods.Http.Post, "pwl-yandex/api/passport/track/check-phone-confirmation")]
    public class YCheckPhoneConfirmationBuilder : YPassportRequestBuilder<YCheckPhoneConfirmation, string>
    {
        public YCheckPhoneConfirmationBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(string tuple)
        {
            return GetJsonContent(new {
                track_id = storage.AuthToken.TrackId
            });
        }
    }
}