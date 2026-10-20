using System.Collections.Specialized;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Search;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Search
{
    [YApiRequest(WebRequestMethods.Http.Get, "search/suggest")]
    public class YSearchSuggestBuilder : YRequestBuilder<YResponse<YSearchSuggest>, string>
    {
        public YSearchSuggestBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override NameValueCollection GetQueryParams(string searchText)
        {
            return new NameValueCollection {
                { "part", searchText }
            };
        }
    }
}