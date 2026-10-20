using System.Collections.Generic;

using YamBassPlayer.Api.Models.Album;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventAlbums: YFeedEventTitled
    {
        public List<YAlbum> Albums { get; set; }
    }
}