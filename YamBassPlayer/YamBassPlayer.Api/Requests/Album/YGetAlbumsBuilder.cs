using System.Collections.Generic;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Album;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Album
{
    [YApiRequest(WebRequestMethods.Http.Post, "albums")]
    public class YGetAlbumsBuilder : YRequestBuilder<YResponse<List<YAlbum>>, IEnumerable<string>>
    {
        public YGetAlbumsBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(IEnumerable<string> albumIds)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "album-ids", string.Join(",", albumIds) }
            });
        }
    }
}