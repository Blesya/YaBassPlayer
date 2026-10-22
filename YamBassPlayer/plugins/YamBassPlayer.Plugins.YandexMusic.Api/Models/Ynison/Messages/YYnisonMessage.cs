using System;

namespace YamBassPlayer.Api.Models.Ynison.Messages
{
    public class YYnisonMessage
    {
        public string Rid { get; set; } = Guid.NewGuid().ToString();
    }
}