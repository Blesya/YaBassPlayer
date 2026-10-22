using System;

using YamBassPlayer.Api.Models.Playlist;

namespace YamBassPlayer.Api.Models.Library
{
    public class YLibraryPlaylists
    {
        public YPlaylist Playlist { get; set; }
        public DateTime Timestamp { get; set; }
    }
}