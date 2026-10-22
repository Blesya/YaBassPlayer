using YamBassPlayer.Extensions;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Продовая реализация: показывает модальный диалог через
/// <see cref="ExceptionExtensions.Handle"/>.
/// </summary>
public sealed class UiErrorHandler : IErrorHandler
{
    public void Handle(Exception exception) => exception.Handle();
}
