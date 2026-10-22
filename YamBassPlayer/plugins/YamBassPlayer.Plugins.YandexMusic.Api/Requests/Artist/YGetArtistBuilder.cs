using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Artist;
using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Artist
{
    [YApiRequest(WebRequestMethods.Http.Get, "artists/{artistId}/brief-info")]
    public class YGetArtistBuilder: YRequestBuilder<YResponse<YArtistBriefInfo>, string>
    {
        public YGetArtistBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
        {
        }

        protected override Dictionary<string, string> GetSubstitutions(string artistId)
        {
            return new Dictionary<string, string> {
                { "artistId", artistId }
            };
        }
    }
}