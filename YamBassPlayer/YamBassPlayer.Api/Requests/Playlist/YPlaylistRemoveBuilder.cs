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
    [YApiRequest(WebRequestMethods.Http.Post, "users/{uid}/playlists/{kind}/name")]
    public class YPlaylistRenameBuilder : YRequestBuilder<YResponse<YPlaylist>, (string kind, string name)>
    {
        public YPlaylistRenameBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((string kind, string name) tuple)
        {
            return new Dictionary<string, string> {
                { "uid", storage.User.Uid },
                { "kind", tuple.kind }
            };
        }

        protected override HttpContent GetContent((string kind, string name) tuple)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "value", tuple.name }
            });
        }
    }
}