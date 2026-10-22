using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services.Impl;

/// <summary>Кнопки действий над треком, зарегистрированные плагинами.</summary>
public sealed class TrackActionRegistry
{
    private readonly List<ITrackAction> _actions = [];

    public IReadOnlyList<ITrackAction> Actions => _actions;

    public event Action? Changed;

    public void Add(ITrackAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_actions.Any(existing => string.Equals(existing.Id, action.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Track action '{action.Id}' is registered more than once.");

        _actions.Add(action);
        action.Changed += () => Changed?.Invoke();
        Changed?.Invoke();
    }
}
