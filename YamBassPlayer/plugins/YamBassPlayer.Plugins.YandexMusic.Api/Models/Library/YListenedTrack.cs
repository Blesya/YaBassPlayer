using System;
using YamBassPlayer.Api.Models.Common;

namespace YamBassPlayer.Api.Models.Library
{
    public class YListenedTrack
    {
        public YTrackId TrackId { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}