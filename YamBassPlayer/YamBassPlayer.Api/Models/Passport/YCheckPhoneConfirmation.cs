using Newtonsoft.Json;

namespace YamBassPlayer.Api.Models.Passport
{
    public class YCheckPhoneConfirmation
    {
        [JsonProperty("isPhoneConfirmed")]
        public bool IsPhoneConfirmed { get; set; }
    }
}