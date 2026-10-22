namespace YamBassPlayer.Plugins;

/// <summary>Пункт меню плагина; хост показывает его в «Плагины → &lt;Плагин&gt;».</summary>
public sealed record PluginMenuEntry(
    string Title,
    Func<Task> Invoke,
    bool IsEnabled = true,
    string? Shortcut = null);

/// <summary>
/// Пункты меню плагина. Хост запрашивает их при каждом открытии меню,
/// поэтому состояние «Войти/Выйти» не требует отдельного события.
/// </summary>
public interface IMenuContributor
{
    IReadOnlyList<PluginMenuEntry> GetEntries();

    event Action? Changed;
}
