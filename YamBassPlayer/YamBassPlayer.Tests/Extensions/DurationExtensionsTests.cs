using YamBassPlayer.Extensions;

namespace YamBassPlayer.Tests.Extensions;

[TestFixture]
public sealed class DurationExtensionsTests
{
    [TestCase(null, "")]
    [TestCase(0L, "")]
    [TestCase(-1000L, "")]
    [TestCase(1000L, "0:01")]
    [TestCase(59_000L, "0:59")]
    [TestCase(60_000L, "1:00")]
    [TestCase(225_000L, "3:45")]
    [TestCase(599_000L, "9:59")]
    [TestCase(600_000L, "10:00")]
    [TestCase(3_599_000L, "59:59")]
    [TestCase(3_600_000L, "1:00:00")]
    [TestCase(3_661_000L, "1:01:01")]
    public void ToShortDuration_ReturnsExpected(long? milliseconds, string expected)
    {
        var result = milliseconds.ToShortDuration();
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ToShortDuration_LongMaxValue_DoesNotThrow()
    {
        long? maxValue = long.MaxValue;
        Assert.DoesNotThrow(() => maxValue.ToShortDuration());
    }
}
