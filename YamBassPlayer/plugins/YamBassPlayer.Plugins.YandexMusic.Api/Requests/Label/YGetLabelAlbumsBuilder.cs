using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Label;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Label
{
    [YApiRequest(WebRequestMethods.Http.Get, "labels/{labelId}/albums")]
    public class YGetLabelAlbumsBuilder : YRequestBuilder<YResponse<YLabelAlbums>, (YLabel label, int pageNumber)>
    {
        public YGetLabelAlbumsBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override NameValueCollection GetQueryParams((YLabel label, int pageNumber) tuple)
        {
            return new NameValueCollection {
                { "page", tuple.pageNumber.ToString() }
            };
        }

        protected override Dictionary<string, string> GetSubstitutions((YLabel label, int pageNumber) tuple)
        {
            return new Dictionary<string, string> {
                { "labelId", tuple.label.Id }
            };
        }
    }
}
