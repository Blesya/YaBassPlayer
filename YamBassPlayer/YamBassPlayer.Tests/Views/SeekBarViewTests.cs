using Terminal.Gui;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class SeekBarViewTests : ViewTestBase
{
    // width - 1 = 50, поэтому колонка = половина процента от ширины.
    private const int Width = 51;

    private SeekBarView CreateView(int width = Width)
        => AddToTop(new SeekBarView { Width = width, Height = 1 });

    private static MouseEvent Click(int x)
        => new() { X = x, Y = 0, Flags = MouseFlags.Button1Clicked };

    // ── Преобразование клика в процент ────────────────────────────────────

    [TestCase(0, 51, 0)]
    [TestCase(25, 51, 50)]
    [TestCase(50, 51, 100)]
    [TestCase(1, 51, 2)]
    [TestCase(0, 11, 0)]
    [TestCase(5, 11, 50)]
    [TestCase(10, 11, 100)]
    [TestCase(1, 4, 33)]
    [TestCase(2, 4, 67)]
    [TestCase(0, 1, 0)]
    [TestCase(7, 1, 0)]
    public void PercentForColumn_MapsColumnsToPercent(int column, int width, int expected)
    {
        Assert.That(SeekBarView.PercentForColumn(column, width), Is.EqualTo(expected));
    }

    [TestCase(-5, 0)]
    [TestCase(500, 100)]
    public void PercentForColumn_ClampsOutOfRangeColumns(int column, int expected)
    {
        Assert.That(SeekBarView.PercentForColumn(column, Width), Is.EqualTo(expected));
    }

    // ── Клики ─────────────────────────────────────────────────────────────

    [Test]
    public void Click_RaisesSeekRequestedWithClickedPercent()
    {
        var view = CreateView();
        int? seeked = null;
        view.SeekRequested += percent => seeked = percent;

        view.MouseEvent(Click(25));

        Assert.That(seeked, Is.EqualTo(50));
    }

    [Test]
    public void Click_UpdatesFractionImmediately()
    {
        var view = CreateView();

        view.MouseEvent(Click(25));

        Assert.That(view.Fraction, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void Click_OnLastColumn_SeekesToEnd()
    {
        var view = CreateView();
        int? seeked = null;
        view.SeekRequested += percent => seeked = percent;

        view.MouseEvent(Click(Width - 1));

        Assert.That(seeked, Is.EqualTo(100));
    }

    [Test]
    public void NonLeftClick_DoesNotSeek()
    {
        var view = CreateView();
        int? seeked = null;
        view.SeekRequested += percent => seeked = percent;

        view.MouseEvent(new MouseEvent { X = 25, Y = 0, Flags = MouseFlags.Button2Clicked });

        Assert.That(seeked, Is.Null);
    }

    // ── Отрисовка ─────────────────────────────────────────────────────────

    [Test]
    public void Redraw_DrawsTrackFillAndThumb()
    {
        var view = CreateView();
        view.Fraction = 0.5f;

        view.Redraw(new Rect(0, 0, Width, 1));

        string text = RenderText(1, Width).TrimEnd();
        Assert.Multiple(() =>
        {
            Assert.That(text, Has.Length.EqualTo(Width));
            Assert.That(text[0], Is.EqualTo('█'));
            Assert.That(text[24], Is.EqualTo('█'));
            Assert.That(text[25], Is.EqualTo('◆'));   // головка ровно на половине
            Assert.That(text[26], Is.EqualTo('─'));
            Assert.That(text[Width - 1], Is.EqualTo('─'));
        });
    }

    [Test]
    public void Redraw_AtStart_PutsThumbOnFirstColumn()
    {
        var view = CreateView(10);
        view.Fraction = 0f;

        view.Redraw(new Rect(0, 0, 10, 1));

        Assert.That(RenderText(1, 10).TrimEnd(), Is.EqualTo("◆" + new string('─', 9)));
    }

    [Test]
    public void Redraw_AtEnd_FillsEverythingBeforeThumb()
    {
        var view = CreateView(10);
        view.Fraction = 1f;

        view.Redraw(new Rect(0, 0, 10, 1));

        Assert.That(RenderText(1, 10).TrimEnd(), Is.EqualTo(new string('█', 9) + "◆"));
    }

    [Test]
    public void Fraction_IsClampedToRange()
    {
        var view = CreateView();

        view.Fraction = 5f;
        Assert.That(view.Fraction, Is.EqualTo(1f));

        view.Fraction = -5f;
        Assert.That(view.Fraction, Is.EqualTo(0f));
    }
}
