using System.Collections.Generic;

using YamBassPlayer.Api.Models.Artist;

namespace YamBassPlayer.Api.Models.Radio
{
    public class YStationData
    {
        public List<YArtist> Artists { get; set; }
        public YBrand Brand { get; set; }
        public string Description { get; set; }
        public string ImageUri { get; set; }
        public string Title { get; set; }
    }
}