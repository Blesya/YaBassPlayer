using System.Collections.Generic;

using YamBassPlayer.Api.Models.Artist;
using YamBassPlayer.Api.Models.Search.Track;

namespace YamBassPlayer.Api.Models.Search.Artist
{
    public class YSearchArtistModel: YArtist
    {
        public List<YSearchTrackModel> PopularTracks { get; set; }
        public List<string> Regions { get; set; }
    }
}