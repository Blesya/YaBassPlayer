using System;

namespace YamBassPlayer.Api.Models.Common
{
    public class YErrorResponse: Exception
    {
        public YInvocationInfo InvocationInfo { get; set; }
        public YError Error { get; set; }
    }
}