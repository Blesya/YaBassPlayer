using System.Collections.Generic;

namespace YamBassPlayer.Api.Models.Radio
{
    public class YStationsDashboard
    {
        public string DashboardId { get; set; }
        public bool Pumpkin { get; set; }
        public List<YStation> Stations { get; set; }
    }
}
