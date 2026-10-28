using System.Collections.Concurrent;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

/// <summary>
/// Одновременные запросы с одним ключом должны выполнять операцию один раз:
/// иначе два окна пишут один и тот же файл обложки и падают с IOException.
/// </summary>
[TestFixture]
public sealed class SingleFlightTests
{
    [Test]
    public async Task RunAsync_ConcurrentCalls_InvokeFactoryOnce()
    {
        var cache = new ConcurrentDictionary<string, Lazy<Task<int>>>();
        int calls = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Factory()
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await Task.Delay(50);
            return 42;
        }

        Task<int> first = SingleFlight.RunAsync(cache, "cover", Factory);
        Task<int> second = SingleFlight.RunAsync(cache, "cover", Factory);

        await started.Task;
        int[] results = await Task.WhenAll(first, second);

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(results, Is.All.EqualTo(42));
            Assert.That(cache, Is.Empty);
        });
    }

    [Test]
    public async Task RunAsync_AfterCompletion_RunsFactoryAgain()
    {
        var cache = new ConcurrentDictionary<string, Lazy<Task<int>>>();
        int calls = 0;

        Task<int> Factory()
        {
            calls++;
            return Task.FromResult(calls);
        }

        await SingleFlight.RunAsync(cache, "cover", Factory);
        await SingleFlight.RunAsync(cache, "cover", Factory);

        Assert.That(calls, Is.EqualTo(2));
    }
}
