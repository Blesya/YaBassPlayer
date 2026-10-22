using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventLikeTrack: YFeedEventTracks
    {
        public YTrack LikedTrack { get; set; }
    }
}