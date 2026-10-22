using System.Collections.Generic;

using YamBassPlayer.Api.Models.Artist;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventArtists: YFeedEventTitled
    {
        public List<YArtist> Artists { get; set; }
    }
}