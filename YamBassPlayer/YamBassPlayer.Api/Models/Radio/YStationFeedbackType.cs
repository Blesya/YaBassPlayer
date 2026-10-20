using System.Text.Json.Serialization;

namespace YamBassPlayer.Api.Models.Radio
{
    public enum YStationFeedbackType
    {
        RadioStarted,
        TrackStarted,
        TrackFinished,
        Skip
    }
}