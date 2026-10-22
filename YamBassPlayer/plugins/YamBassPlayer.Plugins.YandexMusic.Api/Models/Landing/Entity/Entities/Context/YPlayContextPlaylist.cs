using YamBassPlayer.Api.Models.Playlist;

namespace YamBassPlayer.Api.Models.Landing.Entity.Entities.Context
{
    public class YPlayContextPlaylist : YPlayContext
    {
        public YPlaylist Payload { get; set; }
    }
}