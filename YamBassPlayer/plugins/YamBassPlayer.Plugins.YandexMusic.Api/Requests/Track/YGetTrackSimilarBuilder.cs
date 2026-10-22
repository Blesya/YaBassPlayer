using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Track;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Track
{
    [YApiRequest(WebRequestMethods.Http.Get, "tracks/{trackId}/similar")]
    public class YGetTrackSimilarBuilder : YRequestBuilder<YResponse<YTrackSimilar>, string>
    {
        public YGetTrackSimilarBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions(string trackId)
        {
            return new Dictionary<string, string> {
                { "trackId", trackId }
            };
        }
    }
}