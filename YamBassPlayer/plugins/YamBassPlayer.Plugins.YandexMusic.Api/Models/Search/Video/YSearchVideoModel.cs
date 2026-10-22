using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Search.Video
{
    public class YSearchVideoModel
    {
        public int Duration { get; set; }
        public string HtmlAutoPlayVideoPlayer { get; set; }
        public List<string> Regions { get; set; }
        public string Text { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Title { get; set; }
        public string YoutubeUrl { get; set; }
    }
}