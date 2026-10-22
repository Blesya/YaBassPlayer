using System.Collections.Generic;

using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Artist
{
    public class YTracksPage
    {
        public YPager Pager { get; set; }

        public List<YTrack> Tracks { get; set; }
    }
}