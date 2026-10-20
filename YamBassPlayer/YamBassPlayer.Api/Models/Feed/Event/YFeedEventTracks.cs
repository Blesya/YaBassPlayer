using System.Collections.Generic;

using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventTracks: YFeedEventTitled
    {
        public List<YTrack> Tracks { get; set; }
    }
}