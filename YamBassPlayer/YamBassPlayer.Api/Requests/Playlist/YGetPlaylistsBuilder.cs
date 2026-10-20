using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Playlist;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Playlist
{
    [YApiRequest(WebRequestMethods.Http.Post, "playlists/list")]
    public class YGetPlaylistsBuilder : YRequestBuilder<YResponse<List<YPlaylist>>, IEnumerable<(string User, string Kind)>>
    {
        public YGetPlaylistsBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(IEnumerable<(string User, string Kind)> playlistIds)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "playlist-Ids", string.Join(",", playlistIds.Select(t => $"{t.User}:{t.Kind}")) }
            });
        }
    }
}