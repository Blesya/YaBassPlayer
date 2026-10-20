using System.Collections.Generic;

using YamBassPlayer.Api.Models.Album;

namespace YamBassPlayer.Api.Models.Search.Album
{
    public class YSearchAlbumModel: YAlbum
    {
        public List<string> AvailableRegions { get; set; }
        public int OriginalReleaseYear { get; set; }
        public List<string> Regions { get; set; }
    }
}