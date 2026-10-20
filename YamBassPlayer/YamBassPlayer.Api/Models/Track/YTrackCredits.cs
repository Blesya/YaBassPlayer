using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Track
{
    /// <summary>
    /// Обёртка ответа со списком участников создания трека.
    /// </summary>
    public class YTrackCredits
    {
        public List<YTrackCredit> Credits { get; set; }
    }
}
