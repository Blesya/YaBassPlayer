using System.Runtime.Serialization;

namespace YamBassPlayer.Api.Models.Pins
{
    public enum YPinType
    {
        [EnumMember(Value = "album_item")]
        Album,

        [EnumMember(Value = "artist_item")]
        Artist,

        [EnumMember(Value = "playlist_item")]
        Playlist,

        [EnumMember(Value = "wave_item")]
        Wave
    }
}