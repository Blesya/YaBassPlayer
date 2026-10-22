using System.Collections.Generic;
using YamBassPlayer.Api.Models.Landing.Entity.Entities.Context;

namespace YamBassPlayer.Api.Models.Library
{
    public class YRecentlyListened
    {
        public string Client { get; set; }
        public YPlayContextType Context { get; set; }
        public string ContextItem { get; set; }
        public List<YListenedTrack> Tracks { get; set; }
    }
}