namespace YamBassPlayer.Plugins;

/// <summary>Результаты последней загрузки плагинов — для меню и диагностики.</summary>
public sealed class PluginLoadReportStore
{
    public IReadOnlyList<PluginLoadReport> Reports { get; private set; } = [];

    public void Set(IReadOnlyList<PluginLoadReport> reports) => Reports = reports;
}
