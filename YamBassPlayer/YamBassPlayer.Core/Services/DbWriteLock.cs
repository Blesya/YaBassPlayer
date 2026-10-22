using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Services;

/// <summary>
/// Serializes all database write operations on the shared SQLite connection.
/// Registered as a singleton so every consumer shares the same semaphore.
/// </summary>
public interface IDbWriteLock
{
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Concrete implementation using a SemaphoreSlim(1, 1).
/// </summary>
public sealed class DbWriteLock : IDbWriteLock, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(initialCount: 1, maxCount: 1);
    private bool _disposed;

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DbWriteLock));

        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _semaphore.Dispose();
        }
    }

    private void Release()
    {
        if (!_disposed)
            _semaphore.Release();
    }

    private sealed class Releaser(DbWriteLock parent) : IDisposable
    {
        public void Dispose() => parent.Release();
    }
}
