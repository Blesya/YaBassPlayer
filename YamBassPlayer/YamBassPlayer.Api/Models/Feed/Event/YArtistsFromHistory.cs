using System.Collections.Generic;

using YamBassPlayer.Api.Models.Artist;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YArtistsFromHistory
    {
        public YArtist Artist { get; set; }
        public List<YArtist> ArtistsFromHistory { get; set; }
    }
}