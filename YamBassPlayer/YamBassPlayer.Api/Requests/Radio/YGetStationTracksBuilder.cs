using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Radio;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Radio
{
    [YApiRequest(WebRequestMethods.Http.Get, "rotor/station/{type}:{tag}/tracks")]
    public class YGetStationTracksBuilder : YRequestBuilder<YResponse<YStationSequence>, (YStationDescription station, string prevTrackId)>
    {
        public YGetStationTracksBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((YStationDescription station, string prevTrackId) tuple)
        {
            return new Dictionary<string, string> {
                { "type", tuple.station.Id.Type },
                { "tag", tuple.station.Id.Tag },
            };
        }

        protected override NameValueCollection GetQueryParams((YStationDescription station, string prevTrackId) tuple)
        {
            NameValueCollection query = new() {
                { "settings2", "true" }
            };

            if (!string.IsNullOrEmpty(tuple.prevTrackId))
                query.Add("queue", tuple.prevTrackId);

            return query;
        }
    }
}