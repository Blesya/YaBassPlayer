using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Playlist;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Playlist
{
    [YApiRequest(WebRequestMethods.Http.Get, "users/{user}/playlists/{kind}")]
    public class YGetPlaylistBuilder : YRequestBuilder<YResponse<YPlaylist>, (string user, string kind)>
    {
        public YGetPlaylistBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((string user, string kind) tuple)
        {
            return new Dictionary<string, string> {
                { "user", tuple.user },
                { "kind", tuple.kind },
            };
        }
    }
}