using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Radio.Restriction
{
    public class YRestrictionEnum: YRestriction
    {
        public List<YRestrictionValue<string>> PossibleValues { get; set; }
    }
}
