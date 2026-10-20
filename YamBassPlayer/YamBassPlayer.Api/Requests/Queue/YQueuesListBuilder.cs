using System.Net;
using System.Net.Http.Headers;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Queue;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Queue
{
    [YApiRequest(WebRequestMethods.Http.Get, "queues")]
    public class YQueuesListBuilder : YRequestBuilder<YResponse<YQueueItemsContainer>, string>
    {
        public YQueuesListBuilder(YandexMusicApi yandex, AuthStorage auth, string device = null) : base(yandex, auth)
        {
            if (device != null)
                this.device = device;   
        }

        protected override void SetCustomHeaders(HttpRequestHeaders headers)
        {
            headers.Add("X-Yandex-Music-Device", device);
        }
    }
}
