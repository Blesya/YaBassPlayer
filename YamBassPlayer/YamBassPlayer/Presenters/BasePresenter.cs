namespace YamBassPlayer.Presenters;

/// <summary>
/// Base class for presenters providing common patterns:
/// - Thread-safe event invocation
/// - Error handling wrapper
/// - Async safety
/// </summary>
/// <typeparam name="TView">The view interface type this presenter manages.</typeparam>
public abstract class BasePresenter<TView> where TView : class
{
    protected TView View { get; }

    protected BasePresenter(TView view)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
    }

    /// <summary>
    /// Safely invokes an event handler, catching and handling exceptions.
    /// </summary>
    protected void SafeInvoke(Action? handler, string eventName)
    {
        try
        {
            handler?.Invoke();
        }
        catch (Exception ex)
        {
            OnError(ex, eventName);
        }
    }

    /// <summary>
    /// Safely invokes an event handler with a parameter.
    /// </summary>
    protected void SafeInvoke<T>(Action<T>? handler, T arg, string eventName)
    {
        try
        {
            handler?.Invoke(arg);
        }
        catch (Exception ex)
        {
            OnError(ex, eventName);
        }
    }

    /// <summary>
    /// Called when an error occurs during event invocation.
    /// Override to customize error handling behavior.
    /// </summary>
    protected virtual void OnError(Exception ex, string context)
    {
        // Default: use the global exception handler from Extensions
        YamBassPlayer.Extensions.ExceptionExtensions.Handle(ex);
    }
}
