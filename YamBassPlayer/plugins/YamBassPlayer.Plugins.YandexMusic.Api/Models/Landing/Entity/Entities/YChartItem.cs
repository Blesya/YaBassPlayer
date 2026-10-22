using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Landing.Entity.Entities
{
    public class YChartItem
    {
        public YTrack Track { get; set; }
        public YChart Chart { get; set; }
    }
}