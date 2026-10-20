using System.Collections.Generic;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Playlist;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Playlist
{
    [YApiRequest(WebRequestMethods.Http.Post, "users/{uid}/playlists/create")]
    public class YPlaylistCreateBuilder: YRequestBuilder<YResponse<YPlaylist>, string>
    {
        public YPlaylistCreateBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions(string name)
        {
            return new Dictionary<string, string> {
                { "uid", storage.User.Uid }
            };
        }

        protected override HttpContent GetContent(string name)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "title", name },
                { "visibility", "public" }
            });
        }
    }
}