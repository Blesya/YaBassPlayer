using Newtonsoft.Json;

namespace YamBassPlayer.Api.Models.Common
{
    public class YBaseModel
    {
        [JsonIgnore]
        public YExecutionContext Context { get; set; }
    }
}
