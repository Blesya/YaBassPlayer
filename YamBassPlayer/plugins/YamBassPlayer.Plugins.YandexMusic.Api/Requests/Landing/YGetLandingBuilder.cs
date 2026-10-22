using System.Collections.Specialized;
using System.Linq;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Landing;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Landing
{
    [YApiRequest(WebRequestMethods.Http.Get, "landing3")]
    public class YGetLandingBuilder: YRequestBuilder<YResponse<YLanding>, YLandingBlockType[]>
    {
        public YGetLandingBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override NameValueCollection GetQueryParams(YLandingBlockType[] tuple)
        {
            string blocks = string.Join(",", tuple
                .Select(b => SerializeJson(b).Replace("\"", string.Empty)));

            return new NameValueCollection {
                { "blocks", blocks }
            };
        }
    }
}