using System.Collections.Generic;

using Newtonsoft.Json;

using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Playlist
{
    public class YPlaylistChange
    {
        public int? At { get; set; }
        public int? From { get; set; }
        [JsonProperty("op")] 
        public YPlaylistChangeType Operation { get; set; }
        public int? To { get; set; }
        public IEnumerable<YTrackAlbumPair> Tracks { get; set; }
    }
}