namespace YamBassPlayer.Tests.Services;

using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class EventBusTests
{
    // ──────────────── Subscribe + Publish ────────────────

    [Test]
    public void SubscribeAndPublish_CallsHandlerWithCorrectEventData()
    {
        var bus = new EventBus();
        string? received = null;

        bus.Subscribe<string>(s => received = s);
        bus.Publish("hello");

        Assert.That(received, Is.EqualTo("hello"));
    }

    // ──────────────── Multiple subscribers ────────────────

    [Test]
    public void Publish_CallsAllSubscribedHandlers()
    {
        var bus = new EventBus();
        var calls = new List<int>();

        bus.Subscribe<int>(_ => calls.Add(1));
        bus.Subscribe<int>(_ => calls.Add(2));
        bus.Subscribe<int>(_ => calls.Add(3));
        bus.Publish(42);

        Assert.That(calls, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    // ──────────────── Unsubscribe ────────────────

    [Test]
    public void Unsubscribe_RemovedHandlerIsNoLongerCalled()
    {
        var bus = new EventBus();
        var calls = new List<int>();

        Action<string> handler = s => calls.Add(1);
        bus.Subscribe(handler);
        bus.Subscribe<string>(_ => calls.Add(2));

        bus.Unsubscribe(handler);
        bus.Publish("test");

        Assert.That(calls, Is.EqualTo(new[] { 2 }));
    }

    // ──────────────── Different event types ────────────────

    [Test]
    public void Publish_DifferentEventTypesDoNotInterfere()
    {
        var bus = new EventBus();
        string? receivedString = null;
        int receivedInt = 0;

        bus.Subscribe<string>(s => receivedString = s);
        bus.Subscribe<int>(i => receivedInt = i);

        bus.Publish("world");

        Assert.Multiple(() =>
        {
            Assert.That(receivedString, Is.EqualTo("world"));
            Assert.That(receivedInt, Is.EqualTo(0));
        });
    }

    // ──────────────── Exception in handler ────────────────

    [Test]
    public void Publish_ExceptionInOneHandler_DoesNotPreventOtherHandlers()
    {
        var bus = new EventBus();
        var calls = new List<int>();

        bus.Subscribe<int>(_ => throw new InvalidOperationException("fail"));
        bus.Subscribe<int>(_ => calls.Add(2));
        bus.Subscribe<int>(_ => calls.Add(3));

        bus.Publish(99);

        Assert.That(calls, Is.EqualTo(new[] { 2, 3 }));
    }
}
