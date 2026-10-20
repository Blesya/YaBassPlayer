using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Common.Cover
{
    public class YCoverMosaic : YCover
    {
        public bool Custom { get; set; }
        public List<string> ItemsUri { get; set; }
    }
}