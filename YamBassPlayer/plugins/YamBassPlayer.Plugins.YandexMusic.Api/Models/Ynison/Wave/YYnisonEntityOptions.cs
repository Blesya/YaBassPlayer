using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Ynison.Wave
{
    public class YYnisonEntityOptions
    {
        public List<YYnisonTrackSource> TrackSources { get; set; }
        public YYnisonWaveEntityOptional WaveEntityOptional { get; set; }
    }
}