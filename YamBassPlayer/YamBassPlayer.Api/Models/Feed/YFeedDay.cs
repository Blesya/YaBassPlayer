using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using YamBassPlayer.Api.Models.Feed.Event;
using YamBassPlayer.Api.Models.Track;

namespace YamBassPlayer.Api.Models.Feed {
    public class YFeedDay
    {
        public DateTime Day { get; set; }
        [JsonConverter(typeof(YFeedEventConverter))]
        public List<YFeedEventTitled> Events { get; set; }
        public List<YTrack> TracksToPlay { get; set; }
        public List<YFeedDayTrackWithAds> TracksToPlayWithAds { get; set; }
    }
}