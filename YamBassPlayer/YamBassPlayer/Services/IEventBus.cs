namespace YamBassPlayer.Services;

/// <summary>
/// Simple in-process event bus for decoupled pub/sub messaging.
/// Replaces scattered Action&lt;T&gt; events across services with a centralized, typed event system.
/// </summary>
public interface IEventBus
{
    /// <summary>Subscribe to events of type <typeparamref name="T"/>.</summary>
    void Subscribe<T>(Action<T> handler);

    /// <summary>Unsubscribe from events of type <typeparamref name="T"/>.</summary>
    void Unsubscribe<T>(Action<T> handler);

    /// <summary>Publish an event to all subscribers of type <typeparamref name="T"/>.</summary>
    void Publish<T>(T eventData);
}
