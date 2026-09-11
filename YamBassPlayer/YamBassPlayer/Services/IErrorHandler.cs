namespace YamBassPlayer.Services;

/// <summary>
/// Единая точка обработки исключений: в проде показывает диалог, в тестах подменяется
/// на «немой» обработчик, чтобы можно было проверять провальные ветки.
/// </summary>
public interface IErrorHandler
{
    void Handle(Exception exception);
}
