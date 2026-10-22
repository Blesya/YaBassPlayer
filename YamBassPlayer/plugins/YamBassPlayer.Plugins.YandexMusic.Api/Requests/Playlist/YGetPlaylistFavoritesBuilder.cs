using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Playlist;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Playlist
{
    [YApiRequest(WebRequestMethods.Http.Get, "users/{uid}/playlists/list")]
    public class YGetPlaylistFavoritesBuilder: YRequestBuilder<YResponse<List<YPlaylist>>, object>
    {
        public YGetPlaylistFavoritesBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions(object tuple)
        {
            return new Dictionary<string, string> {
                { "uid", storage.User.Uid }
            };
        }
    }
}