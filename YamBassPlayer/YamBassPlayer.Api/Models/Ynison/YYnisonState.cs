using System.Collections.Generic;
using YamBassPlayer.Api.Models.Ynison.Messages;

namespace YamBassPlayer.Api.Models.Ynison
{
    public class YYnisonState: YYnisonMessage
    {
        public List<YYnisonDeviceFull> Devices { get; set; }
        public YYnisonPlayerState PlayerState { get; set; }
        public decimal TimestampMs { get; set; }
    }
}