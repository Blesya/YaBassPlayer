using System.Runtime.Serialization;

namespace YamBassPlayer.Api.Models.Playlist
{
    public enum YArtistPlaylistType
    {
        [EnumMember(Value = "TOP")]
        Top,
        [EnumMember(Value = "SIMILAR")]
        Similar
    }
}