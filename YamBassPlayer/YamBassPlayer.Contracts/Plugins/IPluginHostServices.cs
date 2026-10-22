using YamBassPlayer.Services;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Службы хоста, которые разрешено использовать плагинам. Явный список вместо
/// <see cref="IServiceProvider"/>: плагин не может достать из контейнера что угодно,
/// а контракт виден компилятору.
/// </summary>
public interface IPluginHostServices
{
    IErrorHandler Errors { get; }

    /// <summary>Сценарий «Моя волна»: ядро само показывает плейлист и очередь.</summary>
    IMyWaveService MyWave { get; }

    ITrackInfoProvider TrackInfo { get; }

    /// <summary>Текущая очередь: плагину нужен текущий трек для команд.</summary>
    IPlaybackQueue Playback { get; }

    /// <summary>Куда складывать скачанные треки и обложки.</summary>
    string TracksFolder { get; }

    string CoversFolder { get; }
}
