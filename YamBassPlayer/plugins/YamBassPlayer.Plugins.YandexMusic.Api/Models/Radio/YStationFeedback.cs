namespace YamBassPlayer.Api.Models.Radio
{
    internal sealed class YStationFeedback
    {
        public YStationFeedbackType Type { get; set; }
        public string Timestamp { get; set; }
        public string From { get; set; }
        public double TotalPlayedSeconds { get; set; }
        public string TrackId { get; set; }
    }
}