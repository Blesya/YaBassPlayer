using YamBassPlayer.Models;

namespace YamBassPlayer.Tests.Models;

[TestFixture]
public class SourceIdTests
{
    [Test]
    public void AcceptsLowerCaseIdWithDotsAndDashes()
    {
        var sourceId = new SourceId("yandex-music.v2");

        Assert.That(sourceId.Value, Is.EqualTo("yandex-music.v2"));
    }

    [Test]
    public void RejectsEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new SourceId(string.Empty));
    }

    [Test]
    public void RejectsUpperCaseId()
    {
        Assert.Throws<ArgumentException>(() => new SourceId("Yandex"));
    }

    [Test]
    public void RejectsLeadingDotOrDash()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => new SourceId(".yandex"));
            Assert.Throws<ArgumentException>(() => new SourceId("-yandex"));
        });
    }

    [Test]
    public void RejectsUnderscore()
    {
        Assert.Throws<ArgumentException>(() => new SourceId("my_source"));
    }

    [Test]
    public void RejectsIdLongerThan64()
    {
        var tooLong = new string('a', 65);

        Assert.Throws<ArgumentException>(() => new SourceId(tooLong));
    }

    [Test]
    public void DefaultHasNoValue()
    {
        var sourceId = default(SourceId);

        Assert.Multiple(() =>
        {
            Assert.That(sourceId.Value, Is.Null);
            Assert.That(sourceId.IsEmpty, Is.True);
            Assert.That(sourceId.ToString(), Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void EqualityIsByValue()
    {
        var first = new SourceId("yandex");
        var second = new SourceId("yandex");
        var other = new SourceId("local");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(other));
        });
    }
}
