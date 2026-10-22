using System.Collections.Generic;
using YamBassPlayer.Api.Models.Artist;
using YamBassPlayer.Api.Models.Common;

namespace YamBassPlayer.Api.Models.Label
{
    public class YLabelArtists
    {
        public YPager Pager { get; set; }
        public List<YArtist> Artists { get; set; }
    }
}