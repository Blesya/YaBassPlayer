using System.Collections.Generic;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Track;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Track
{
    [YApiRequest(WebRequestMethods.Http.Post, "tracks")]
    public class YGetTracksBuilder : YRequestBuilder<YResponse<List<YTrack>>, IEnumerable<string>>
    {
        public YGetTracksBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(IEnumerable<string> trackIds)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "track-ids", string.Join(",", trackIds) },
                { "with-positions", "true" }
            });
        }
    }
}