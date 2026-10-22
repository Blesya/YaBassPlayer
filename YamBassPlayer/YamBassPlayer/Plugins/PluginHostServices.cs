using YamBassPlayer.Services;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Хост-реализация явных служб для плагинов. Плагин получает только то,
/// что перечислено в <see cref="IPluginHostServices"/>, а не весь контейнер.
/// </summary>
public sealed class PluginHostServices(
    IErrorHandler errors,
    IMyWaveService myWave,
    ITrackInfoProvider trackInfo,
    IPlaybackQueue playback,
    string tracksFolder,
    string coversFolder) : IPluginHostServices
{
    public IErrorHandler Errors => errors;

    public IMyWaveService MyWave => myWave;

    public ITrackInfoProvider TrackInfo => trackInfo;

    public IPlaybackQueue Playback => playback;

    public string TracksFolder => tracksFolder;

    public string CoversFolder => coversFolder;
}
