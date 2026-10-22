using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Artist;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Artist
{
    [YApiRequest(WebRequestMethods.Http.Get, "artists/{artistId}/tracks")]
    public class YGetArtistTrackBuilder: YRequestBuilder<YResponse<YTracksPage>, (string id, int page, int pageSize)>
    {
        public YGetArtistTrackBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth) { }

        protected override Dictionary<string, string> GetSubstitutions((string id, int page, int pageSize) tuple)
        {
            return new Dictionary<string, string> {
                { "artistId", tuple.id },
            };
        }

        protected override NameValueCollection GetQueryParams((string id, int page, int pageSize) tuple)
        {
            return new NameValueCollection {
                { "page", tuple.page.ToString() },
                { "pageSize", tuple.pageSize.ToString() },
            };
        }
    }
}