using System.Collections.Generic;
using System.Net;

using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Artist;
using YamBassPlayer.Api.Requests.Common;
using YamBassPlayer.Api.Requests.Common.Attributes;

namespace YamBassPlayer.Api.Requests.Artist
{
    [YApiRequest(WebRequestMethods.Http.Get, "artists/{artistId}/about-artist")]
    public class YGetArtistAboutBuilder : YRequestBuilder<YArtistAbout, string>
    {
        public YGetArtistAboutBuilder(YandexMusicApi yandex, AuthStorage auth) : base(yandex, auth)
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
