using System.Collections.Generic;
using System.Net;
using System.Net.Http;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Artist;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Artist
{
    [YApiRequest(WebRequestMethods.Http.Post, "artists")]
    public class YGetArtistsBuilder : YRequestBuilder<YResponse<List<YArtist>>, IEnumerable<string>>
    {
        public YGetArtistsBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override HttpContent GetContent(IEnumerable<string> artistIds)
        {
            return new FormUrlEncodedContent(new Dictionary<string, string> {
                { "artist-Ids", string.Join(",", artistIds) }
            });
        }
    }
}