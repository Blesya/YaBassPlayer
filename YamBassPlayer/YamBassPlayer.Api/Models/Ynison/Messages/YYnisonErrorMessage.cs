using System;

namespace YamBassPlayer.Api.Models.Ynison.Messages
{
    public class YYnisonErrorMessage: Exception
    {
        public YYnisonError Error { get; set; }
    }
}