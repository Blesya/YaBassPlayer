using System.Collections.Generic;

using YamBassPlayer.Api.Models.Artist;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventSimilarArtists: YFeedEventTitled
    {
        public YArtist SimilarToArtist { get; set; }
        public List<YArtist> SimilarArtists { get; set; }
    }
}