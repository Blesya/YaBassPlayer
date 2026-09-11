using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class TrackSourceDetector : ITrackSourceDetector
{
    public string GetSourceId(string trackId)
        => IsLocal(trackId) ? SourceIds.Local : SourceIds.Yandex;

    public bool IsLocal(string trackId)
        => !string.IsNullOrEmpty(trackId) && Path.IsPathRooted(trackId);
}
