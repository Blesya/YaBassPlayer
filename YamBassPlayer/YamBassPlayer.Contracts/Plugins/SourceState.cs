namespace YamBassPlayer.Plugins;

/// <summary>Готовность источника: от неё зависит узел дерева и доступность пунктов меню.</summary>
public enum SourceState
{
    Ready,
    RequiresSetup,
    Unavailable
}
