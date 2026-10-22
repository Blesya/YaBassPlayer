using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Radio;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Radio
{
    [YApiRequest(WebRequestMethods.Http.Get, "rotor/stations/dashboard")]
    public class YGetStationsDashboardBuilder : YRequestBuilder<YResponse<YStationsDashboard>, object>
    {
        public YGetStationsDashboardBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}