using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services.Impl;

/// <summary>Пункты меню одного плагина.</summary>
public sealed record PluginMenuGroup(IPlugin Plugin, IMenuContributor Menu)
{
    public string PluginId => Plugin.Id;
    public string DisplayName => Plugin.DisplayName;
}

/// <summary>
/// Собирает меню «Плагины» из вкладов загруженных плагинов.
/// </summary>
public sealed class MenuRegistry
{
    private readonly List<PluginMenuGroup> _groups = [];

    public IReadOnlyList<PluginMenuGroup> Groups => _groups;

    public void Add(IPlugin plugin, IMenuContributor menu)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(menu);

        _groups.Add(new PluginMenuGroup(plugin, menu));
    }
}
