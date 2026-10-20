using YamBassPlayer.Api.Models.Artist;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventArtist: YFeedEventTracks
    {
        public YArtist Artist { get; set; }
    }
}