namespace YamBassPlayer.Services;

/// <summary>
/// Detects the source type of a track based on its ID.
/// Local tracks use file paths as IDs (rooted paths), while remote tracks use external IDs.
/// </summary>
public interface ITrackSourceDetector
{
    /// <summary>Returns "local" if the trackId is a file path, otherwise "yandex".</summary>
    string GetSourceId(string trackId);

    /// <summary>Returns true if the trackId represents a local file.</summary>
    bool IsLocal(string trackId);
}
