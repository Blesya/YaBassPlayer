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
    [YApiRequest(WebRequestMethods.Http.Post, "users/{uid}/playlists/{kind}/change")]
    public class YPlaylistChangeBuilder: YRequestBuilder<YResponse<YPlaylist>, (YPlaylist playlist, IEnumerable<YPlaylistChange> changes)>
    {
        public YPlaylistChangeBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions((YPlaylist playlist, IEnumerable<YPlaylistChange> changes) tuple)
        {
            return new Dictionary<string, string> {
                { "uid", storage.User.Uid },
                { "kind", tuple.playlist.Kind }
            };
        }

        protected override HttpContent GetContent((YPlaylist playlist, IEnumerable<YPlaylistChange> changes) tuple)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "kind", tuple.playlist.Kind },
                { "revision", tuple.playlist.Revision.ToString() },
                { "diff", SerializeJson(tuple.changes) }
            });
        }
    }
}