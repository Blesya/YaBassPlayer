using System.Collections.Generic;

using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Library
{
    public class YRecentlyListenedContext
    {
        public List<YRecentlyListened> Contexts { get; set; }
        public List<YTrack> OtherTracks { get; set; }
    }
}