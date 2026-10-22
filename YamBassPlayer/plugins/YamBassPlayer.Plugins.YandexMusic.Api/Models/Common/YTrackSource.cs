using System.ComponentModel;
using System.Runtime.Serialization;

namespace YamBassPlayer.Api.Models.Common
{
    public enum YTrackSource
    {
        [EnumMember(Value = "OWN")]
        Own,
        
        [EnumMember(Value = "UGC")]
        [Description("User Generated Content")]
        UGC,
        
        [EnumMember(Value = "OWN_REPLACED_TO_UGC")]
        OwnReplacedToUGC,
    }
}