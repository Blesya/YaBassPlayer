using System.Runtime.Serialization;

namespace YamBassPlayer.Api.Models.Common
{
    public enum YTrackSharingFlag
    {
        [EnumMember(Value = "VIDEO_ALLOWED")]
        VideoAllowed,
        [EnumMember(Value = "COVER_ONLY")]
        CoverOnly
    }
}
