using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Ynison.Wave
{
    public class YYnisonWaveQueue
    {
        public List<YYnisonPlayableItem> RecommendedPlayableList { get; set; }
        public int LivePlayableIndex { get; set; }
        public YYnisonEntityOptions EntityOptions { get; set; }
    }
}