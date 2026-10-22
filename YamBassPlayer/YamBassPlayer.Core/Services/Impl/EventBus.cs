using System.Collections.Concurrent;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Thread-safe in-process event bus implementation.
/// Uses ConcurrentDictionary to store subscribers per event type.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _lock = new();

    public void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(type, out var handlers))
            {
                handlers = [];
                _subscribers[type] = handlers;
            }
            handlers.Add(handler);
        }
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        lock (_lock)
        {
            if (_subscribers.TryGetValue(type, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                    _subscribers.TryRemove(type, out _);
            }
        }
    }

    public void Publish<T>(T eventData)
    {
        var type = typeof(T);
        List<Delegate>? handlers;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(type, out handlers))
                return;
            // Snapshot to avoid issues if handlers modify subscriptions during iteration
            handlers = [.. handlers];
        }

        foreach (var handler in handlers)
        {
            if (handler is Action<T> typedHandler)
            {
                try
                {
                    typedHandler(eventData);
                }
                catch
                {
                    // Swallow exceptions to prevent one subscriber from breaking others
                }
            }
        }
    }
}
