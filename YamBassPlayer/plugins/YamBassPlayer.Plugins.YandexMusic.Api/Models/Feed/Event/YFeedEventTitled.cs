using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Feed.Event
{
    public class YFeedEventTitled: YFeedEvent
    {
        public List<YFeedEventTitle> Title { get; set; }
        public YFeedEventType TypeForFrom { get; set; }
    }
}