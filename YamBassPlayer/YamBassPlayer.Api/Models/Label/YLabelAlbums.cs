using System.Collections.Generic;
using YamBassPlayer.Api.Models.Album;
using YamBassPlayer.Api.Models.Common;

namespace YamBassPlayer.Api.Models.Label
{
    public class YLabelAlbums
    {
        public YPager Pager { get; set; }
        public List<YAlbum> Albums { get; set; }
    }
}