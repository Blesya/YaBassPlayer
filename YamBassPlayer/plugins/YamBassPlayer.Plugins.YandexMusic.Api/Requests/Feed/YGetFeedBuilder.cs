using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Feed;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Feed
{
    [YApiRequest(WebRequestMethods.Http.Get, "feed")]
    public class YGetFeedBuilder : YRequestBuilder<YResponse<YFeed>, object>
    {
        public YGetFeedBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }
    }
}