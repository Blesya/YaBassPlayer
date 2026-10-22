using YamBassPlayer.Models;

namespace YamBassPlayer.Tests.Models;

[TestFixture]
public class TrackKeyTests
{
    [Test]
    public void ToStringJoinsSourceAndTrack()
    {
        var key = new TrackKey(new SourceId("yandex"), "987654");

        Assert.That(key.ToString(), Is.EqualTo("yandex:987654"));
    }

    [Test]
    public void KeysWithSameTrackIdAndDifferentSourcesAreDifferent()
    {
        var yandexKey = new TrackKey(new SourceId("yandex"), "12345");
        var localKey = new TrackKey(new SourceId("local"), "12345");

        Assert.That(yandexKey, Is.Not.EqualTo(localKey));
    }

    [Test]
    public void EqualityIsByValue()
    {
        var first = new TrackKey(new SourceId("yandex"), "987654");
        var second = new TrackKey(new SourceId("yandex"), "987654");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.TrackId, Is.EqualTo("987654"));
            Assert.That(first.SourceId, Is.EqualTo(new SourceId("yandex")));
        });
    }
}
