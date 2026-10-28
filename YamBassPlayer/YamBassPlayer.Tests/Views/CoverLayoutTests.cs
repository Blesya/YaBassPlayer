using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

/// <summary>
/// Раскладка полноэкранной обложки: она должна помещаться в доступную площадь
/// и расти вместе с окном, не теряя пропорций.
/// </summary>
[TestFixture]
public sealed class CoverLayoutTests
{
    [TestCase(76, 20)]
    [TestCase(145, 47)]
    [TestCase(200, 60)]
    public void Compute_FitsAvailableArea(int width, int height)
    {
        var (coverWidth, coverHeight) = CoverLayout.Compute(width, height);

        Assert.Multiple(() =>
        {
            Assert.That(coverWidth, Is.InRange(1, width));
            Assert.That(coverHeight, Is.InRange(1, height));
        });
    }

    [Test]
    public void Compute_LargerArea_GivesLargerCover()
    {
        var small = CoverLayout.Compute(76, 20);
        var large = CoverLayout.Compute(145, 47);

        Assert.Multiple(() =>
        {
            Assert.That(large.Width, Is.GreaterThan(small.Width));
            Assert.That(large.Height, Is.GreaterThan(small.Height));
        });
    }

    [Test]
    public void Compute_KeepsCoverAspect()
    {
        var (width, height) = CoverLayout.Compute(145, 47);

        Assert.That(width / (double)height, Is.EqualTo(20 / 11.0).Within(0.5));
    }

    [Test]
    public void Compute_TinyArea_StillReturnsPositiveSize()
    {
        var (width, height) = CoverLayout.Compute(20, 8);

        Assert.Multiple(() =>
        {
            Assert.That(width, Is.GreaterThan(0));
            Assert.That(height, Is.GreaterThan(0));
        });
    }
}
