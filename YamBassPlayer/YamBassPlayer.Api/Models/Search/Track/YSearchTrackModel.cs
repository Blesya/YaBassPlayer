using System.Collections.Generic;

using YamBassPlayer.Api.Models.Search.Album;
using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Search.Track
{
    public class YSearchTrackModel: YTrack
    {
        public new List<YSearchAlbumModel> Albums { get; set; }
        public bool AvailableAsRbt { get; set; }
        public bool Explicit { get; set; }
        public List<string> Regions { get; set; }
    }
}