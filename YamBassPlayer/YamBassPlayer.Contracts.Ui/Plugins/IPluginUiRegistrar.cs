namespace YamBassPlayer.Plugins;

/// <summary>
/// UI-часть регистратора: только плагины, которым нужен Terminal.Gui,
/// ссылаются на Contracts.Ui и получают её приведением регистратора.
/// </summary>
public interface IPluginUiRegistrar
{
    IPluginUi Ui { get; }
}
