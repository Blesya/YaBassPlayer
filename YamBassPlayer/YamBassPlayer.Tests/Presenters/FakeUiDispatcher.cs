using YamBassPlayer.Services;

namespace YamBassPlayer.Tests.Presenters;

/// <summary>
/// Синхронный <see cref="IUiDispatcher"/> для тестов: <c>Invoke</c> выполняется сразу,
/// таймер сохраняется и «тикает» вручную через <see cref="Tick"/>.
/// </summary>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    private Func<bool>? _timer;

    public void Invoke(Action action) => action();

    public void StartTimer(TimeSpan interval, Func<bool> onTick) => _timer = onTick;

    /// <summary>Один тик таймера; возвращает <c>false</c>, если таймер остановился.</summary>
    public bool Tick() => _timer is null || _timer();
}
