using Newtonsoft.Json;

using YamBassPlayer.Api.Models.Radio.Restriction;

namespace YamBassPlayer.Api.Models.Radio
{
    public class YStationRestrictions2
    {
        [JsonConverter(typeof(YRestrictionConverter))]
        public YRestriction Diversity { get; set; }
        [JsonConverter(typeof(YRestrictionConverter))]
        public YRestriction Language { get; set; }
        [JsonConverter(typeof(YRestrictionConverter))]
        public YRestriction MoodEnergy { get; set; }
    }
}