using System.Net;

namespace YamBassPlayer.Api.Extensions
{
    public static class HttpRequestHeaderExtensions
    {
        public static string GetName(this HttpRequestHeader header)
        {
            return header.ToString().SplitByCapitalLetter("-");
        }
    }
}