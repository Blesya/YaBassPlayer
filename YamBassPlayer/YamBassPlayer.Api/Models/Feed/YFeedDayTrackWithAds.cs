using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Feed
{
    public class YFeedDayTrackWithAds
    {
        public YFeedDayTrackWithAdsType Type { get; set; }
        public YTrack Track { get; set; }
    }
}