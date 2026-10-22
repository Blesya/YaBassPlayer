using Newtonsoft.Json;

namespace YamBassPlayer.Api.Models.Passport
{
    public class YPassportSession
    {
        [JsonProperty("track_id")]
        public string TrackId { get; set; }

        [JsonProperty("default_uid")]
        public string DefaultUid { get; set; }
    }
}