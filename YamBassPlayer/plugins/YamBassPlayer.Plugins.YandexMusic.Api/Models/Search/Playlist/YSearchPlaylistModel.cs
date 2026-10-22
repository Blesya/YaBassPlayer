using System.Collections.Generic;

using YamBassPlayer.Api.Models.Playlist;

namespace YamBassPlayer.Api.Models.Search.Playlist
{
    public class YSearchPlaylistModel: YPlaylist
    {
        public string CoverUri { get; set; }
        public List<string> Regions { get; set; }
    }
}