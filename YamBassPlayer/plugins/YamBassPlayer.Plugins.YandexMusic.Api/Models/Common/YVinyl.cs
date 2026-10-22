using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Common
{
    public class YVinyl
    {
        public List<string> ArtistIds { get; set; }
        public string Media { get; set; }
        public string OfferId { get; set; }
        public string Picture { get; set; }
        public decimal Price { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public int Year { get; set; }
    }
}