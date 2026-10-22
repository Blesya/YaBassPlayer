using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Radio
{
    public class YSequenceItem
    {
        public bool Liked { get; set; }
        public YTrack Track { get; set; }
        public YTrackParameters TrackParameters { get; set; }
        public string Type { get; set; }
    }
}
