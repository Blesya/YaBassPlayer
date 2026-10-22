using YamBassPlayer.Commands;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Регистрация вкладов плагина. Хост собирает их в свои реестры, контейнер
/// плагину не показывается.
/// </summary>
public interface IPluginRegistrar
{
    void AddMusicSource(IMusicSource source);

    void AddMenu(IMenuContributor menu);

    void AddCommand(ICommand command);

    void AddTrackAction(ITrackAction action);

    IPluginStateStore State { get; }

    IPluginSettings Settings { get; }

    IPluginLogger Log { get; }

    IEventBus Events { get; }

    /// <summary>Явные службы хоста, доступные плагину.</summary>
    IPluginHostServices Host { get; }
}
