using YamBassPlayer.Api.Models.Album;

namespace YamBassPlayer.Api.Models.Landing.Entity.Entities.Context
{
    public class YPlayContextAlbum: YPlayContext
    {
        public YAlbum Payload { get; set; }
    }
}