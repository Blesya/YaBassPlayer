using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Queue;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Queue
{
    [YApiRequest(WebRequestMethods.Http.Get, "queues/{queueId}")]
    public class YGetQueueBuilder : YRequestBuilder<YResponse<YQueue>, string>
    {
        public YGetQueueBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions(string queueId)
        {
            return new Dictionary<string, string> {
                { "queueId", queueId }
            };
        }
    }
}
