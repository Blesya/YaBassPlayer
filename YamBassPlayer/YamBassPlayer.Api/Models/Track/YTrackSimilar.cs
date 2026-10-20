using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Track
{
    public class YTrackSimilar
    {
        public YTrack Track { get; set; }
        public List<YTrack> SimilarTracks { get; set; }
    }
}