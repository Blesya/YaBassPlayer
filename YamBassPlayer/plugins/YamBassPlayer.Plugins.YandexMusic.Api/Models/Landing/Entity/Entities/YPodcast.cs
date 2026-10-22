using System;

using YamBassPlayer.Api.Models.Album;

namespace YamBassPlayer.Api.Models.Landing.Entity.Entities
{
    public class YPodcast
    {
        public string Description { get; set; }
        public string DescriptionFormatted { get; set; }
        public DateTime LastUpdated { get; set; }
        public YAlbum Podcast { get; set; }
    }
}