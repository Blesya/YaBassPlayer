using YamBassPlayer.Commands;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Plugins;

/// <summary>Регистратор вкладов одного плагина поверх реестров хоста.</summary>
public sealed class PluginRegistrar(
    IPlugin plugin,
    MusicSourceRegistry sources,
    MenuRegistry menus,
    CommandRegistry commands,
    TrackActionRegistry actions,
    IEventBus events,
    IPluginStateFile stateFile,
    IPluginUi ui,
    IPluginHostServices host) : IPluginRegistrar, IPluginUiRegistrar
{
    public IPluginStateStore State { get; } = new PluginStateStore(stateFile, plugin.Id);

    public IPluginSettings Settings { get; } = new PluginSettings(plugin.Id);

    public IPluginLogger Log { get; } = new PluginLogger(plugin.Id);

    public IPluginUi Ui => ui;

    public IEventBus Events => events;

    public IPluginHostServices Host => host;

    public void AddMusicSource(IMusicSource source) => sources.Add(source);

    public void AddMenu(IMenuContributor menu) => menus.Add(plugin, menu);

    public void AddCommand(ICommand command) => commands.Add(command);

    public void AddTrackAction(ITrackAction action) => actions.Add(action);
}
