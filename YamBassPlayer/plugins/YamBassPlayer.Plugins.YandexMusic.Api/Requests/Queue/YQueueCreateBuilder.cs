using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Queue;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Queue
{
    [YApiRequest(WebRequestMethods.Http.Post, "queues")]
    public class YQueueCreateBuilder : YRequestBuilder<YResponse<YNewQueue>, YQueue>
    {
        public YQueueCreateBuilder(YandexMusicApi yandex, AuthStorage auth, string device = null) : base(yandex, auth)
        {
            if (device != null)
                this.device = device;   
        }

        protected override HttpContent GetContent(YQueue queue)
        {
            JsonSerializerOptions settings = new() {
                Converters = {
                    new JsonStringEnumConverter()
                },
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonContent.Create(queue, new MediaTypeHeaderValue("application/json"), settings);
        }
        
        protected override void SetCustomHeaders(HttpRequestHeaders headers)
        {
            headers.Add("X-Yandex-Music-Device", device);
        }
    }
}
