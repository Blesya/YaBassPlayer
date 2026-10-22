using System.Collections.Generic;

using Newtonsoft.Json;
using YamBassPlayer.Api.Models.Pins.Items;

namespace YamBassPlayer.Api.Models.Pins
{
    public class YPins
    {
        [JsonConverter(typeof(YPinConverter))]
        public List<YPin> Pins { get; set; }
    }
}