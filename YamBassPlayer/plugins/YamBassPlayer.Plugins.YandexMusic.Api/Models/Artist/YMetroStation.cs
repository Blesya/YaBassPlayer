using Newtonsoft.Json;

namespace YamBassPlayer.Api.Models.Artist
{
    public class YMetroStation
    {
        [JsonProperty("line-color")]
        public string LineColor { get; set; }
        public string Title { get; set; }
    }
}