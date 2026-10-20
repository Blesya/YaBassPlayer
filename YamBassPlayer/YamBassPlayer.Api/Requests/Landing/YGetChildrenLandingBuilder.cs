using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Landing;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Landing
{
    [YApiRequest(WebRequestMethods.Http.Get, "children-landing/catalogue")]
    public class YGetChildrenLandingBuilder : YRequestBuilder<YResponse<YChildrenLanding>, object>
    {
        public YGetChildrenLandingBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}