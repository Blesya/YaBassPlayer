using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Radio;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Radio
{
    [YApiRequest(WebRequestMethods.Http.Get, "rotor/station/{type}:{tag}/info")]
    public class YGetStationBuilder : YRequestBuilder<YResponse<List<YStation>>, (string type, string tag)>
    {
        public YGetStationBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((string type, string tag) tuple)
        {
            return new Dictionary<string, string> {
                { "type", tuple.type },
                { "tag", tuple.tag }
            };
        }
    }
}