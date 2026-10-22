using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Playlist;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Playlist
{
    [YApiRequest(WebRequestMethods.Http.Get, "playlist/{uuid}")]
    public class YGetPlaylistByUuidBuilder : YRequestBuilder<YResponse<YPlaylist>, string>
    {
        public YGetPlaylistByUuidBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions(string uuid)
        {
            return new Dictionary<string, string> {
                { "uuid", uuid },
            };
        }
    }
}