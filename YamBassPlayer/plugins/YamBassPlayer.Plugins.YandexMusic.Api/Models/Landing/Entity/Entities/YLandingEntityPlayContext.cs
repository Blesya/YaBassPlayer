using Newtonsoft.Json;

using YamBassPlayer.Api.Models.Landing.Entity.Entities.Context;

namespace YamBassPlayer.Api.Models.Landing.Entity.Entities
{
    public class YLandingEntityPlayContext: YLandingEntity
    {
        [JsonConverter(typeof(YPlayContextConverter))]
        public YPlayContext Data { get; set; }
    }
}