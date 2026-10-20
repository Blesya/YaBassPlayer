using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Album;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Album
{
    [YApiRequest(WebRequestMethods.Http.Get, "albums/{albumId}/with-tracks")]
    public class YGetAlbumBuilder: YRequestBuilder<YResponse<YAlbum>, string>
    {
        public YGetAlbumBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions(string albumId)
        {
            return new Dictionary<string, string> {
                { "albumId", albumId }
            };
        }
    }
}