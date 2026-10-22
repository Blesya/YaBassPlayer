using YamBassPlayer.Api.Models.Artist;

namespace YamBassPlayer.Api.Models.Landing.Entity.Entities.Context
{
    public class YPlayContextArtist : YPlayContext
    {
        public YArtist Payload { get; set; }
    }
}