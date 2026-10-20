using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Track
{
    [YApiRequest(WebRequestMethods.Http.Get, "tracks/{trackKey}/download-info")]
    public class YTrackDownloadInfoBuilder : YRequestBuilder<YResponse<List<YTrackDownloadInfo>>, (string trackKey, bool direct)>
    {
        public YTrackDownloadInfoBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((string trackKey, bool direct) tuple)
        {
            return new Dictionary<string, string> {
                { "trackKey", tuple.trackKey }
            };
        }

        protected override NameValueCollection GetQueryParams((string trackKey, bool direct) tuple)
        {
            return new NameValueCollection {
                { "direct", tuple.direct.ToString() }
            };
        }
    }
}