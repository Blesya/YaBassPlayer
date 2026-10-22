using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventArtistWithArtists: YFeedEventTitled
    {
        public List<YArtistsFromHistory> ArtistsWithArtistsFromHistory { get; set; }
    }
}