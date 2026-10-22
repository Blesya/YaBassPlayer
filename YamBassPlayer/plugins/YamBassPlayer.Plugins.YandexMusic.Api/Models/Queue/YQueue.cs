using System.Collections.Generic;

using YamBassPlayer.Api.Models.Common;

namespace YamBassPlayer.Api.Models.Queue
{
    public class YQueue
    {
        public string Id { get; set; }
        public YContext Context { get; set; }
        public List<YTrackId> Tracks { get; set; }
        public int? CurrentIndex { get; set; }
        public string Modified { get; set; }
        public string From { get; set; }
        public bool IsInteractive { get; set; }
    }
}
