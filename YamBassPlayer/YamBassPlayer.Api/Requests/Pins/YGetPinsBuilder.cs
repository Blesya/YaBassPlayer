using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Pins;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Pins
{
    [YApiRequest(WebRequestMethods.Http.Get, "pins")]
    public class YGetPinsBuilder: YRequestBuilder<YResponse<YPins>, object>
    {
        public YGetPinsBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}