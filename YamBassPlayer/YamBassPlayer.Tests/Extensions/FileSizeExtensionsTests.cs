using YamBassPlayer.Extensions;

namespace YamBassPlayer.Tests.Extensions;

[TestFixture]
public sealed class FileSizeExtensionsTests
{
    [TestCase(0, "0 Б")]
    [TestCase(1, "1 Б")]
    [TestCase(500, "500 Б")]
    [TestCase(1023, "1023 Б")]
    [TestCase(1024, "1 КБ")]
    [TestCase(2048, "2 КБ")]
    [TestCase(1536, "1,5 КБ")]
    [TestCase(10_240, "10 КБ")]
    [TestCase(15_360, "15 КБ")]
    [TestCase(1_048_576, "1 МБ")]
    [TestCase(1_572_864, "1,5 МБ")]
    [TestCase(10_485_760, "10 МБ")]
    [TestCase(1_073_741_824, "1 ГБ")]
    [TestCase(1_610_612_736, "1,5 ГБ")]
    [TestCase(10_737_418_240, "10 ГБ")]
    public void ToHumanReadableSize_ReturnsExpectedString(long bytes, string expected)
    {
        var result = bytes.ToHumanReadableSize();
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ToHumanReadableSize_LongMaxValue_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => long.MaxValue.ToHumanReadableSize());
    }
}
