using System.Collections.Generic;

using YamBassPlayer.Api.Models.Common;

namespace YamBassPlayer.Api.Models.Track
{
    public class YTrackSupplement
    {
        public string Id { get; set; }
        public List<YClip> Clips { get; set; }
        public YLyrics Lyrics { get; set; }
        public List<YVideo> Videos { get; set; }
    }
}