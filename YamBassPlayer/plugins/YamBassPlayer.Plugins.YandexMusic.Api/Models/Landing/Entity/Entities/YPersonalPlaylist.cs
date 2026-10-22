using System.Collections.Generic;

using YamBassPlayer.Api.Models.Playlist;

namespace YamBassPlayer.Api.Models.Landing.Entity.Entities
{
    public class YPersonalPlaylist
    {
        public YPlaylist Data { get; set; }
        public List<string> Description { get; set; }
        public bool Notify { get; set; }
        public string PreviewDescription { get; set; }
        public bool Ready { get; set; }
        public string Type { get; set; }
    }
}