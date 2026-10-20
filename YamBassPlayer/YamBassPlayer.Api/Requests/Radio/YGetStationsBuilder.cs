using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Radio;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Radio
{
    [YApiRequest(WebRequestMethods.Http.Get, "rotor/stations/list")]
    public class YGetStationsBuilder : YRequestBuilder<YResponse<List<YStation>>, object>
    {
        public YGetStationsBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}