using Terminal.Gui;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class VolumeBarViewTests : ViewTestBase
{
    private const int Width = 76;

    private VolumeBarView CreateView(int width = Width)
        => AddToTop(new VolumeBarView { X = 0, Width = width, Height = 1 });

    private string Render(VolumeBarView view, int width = Width)
    {
        view.Redraw(new Rect(0, 0, width, 1));
        return RenderText(1, width);
    }

    private static MouseEvent Click(int x)
        => new() { X = x, Y = 0, Flags = MouseFlags.Button1Clicked };

    // ── Преобразование клика в процент ────────────────────────────────────

    [TestCase(0, 11, 0)]
    [TestCase(5, 11, 50)]
    [TestCase(10, 11, 100)]
    [TestCase(1, 4, 33)]
    [TestCase(2, 4, 67)]
    [TestCase(0, 1, 0)]
    [TestCase(7, 1, 0)]
    public void PercentForColumn_MapsColumnsToPercent(int column, int barWidth, int expected)
    {
        Assert.That(VolumeBarView.PercentForColumn(column, barWidth), Is.EqualTo(expected));
    }

    [TestCase(-5, 0)]
    [TestCase(500, 100)]
    public void PercentForColumn_ClampsOutOfRangeColumns(int column, int expected)
    {
        Assert.That(VolumeBarView.PercentForColumn(column, 11), Is.EqualTo(expected));
    }

    // ── Клики и колесо ────────────────────────────────────────────────────

    [Test]
    public void Click_OnBar_RaisesVolumeChangeWithClickedPercent()
    {
        var view = CreateView();
        (int start, int barWidth) = view.BarGeometry(Width);
        int? requested = null;
        view.VolumeChangeRequested += percent => requested = percent;

        view.MouseEvent(Click(start + barWidth / 2));

        Assert.That(requested, Is.InRange(48, 52));
        Assert.That(view.Percent, Is.EqualTo(requested));
    }

    [Test]
    public void Click_OnLabel_DoesNotChangeVolume()
    {
        var view = CreateView();
        int? requested = null;
        view.VolumeChangeRequested += percent => requested = percent;

        view.MouseEvent(Click(1));

        Assert.That(requested, Is.Null);
    }

    [Test]
    public void Wheel_ChangesVolumeByStep()
    {
        var view = CreateView();
        view.Percent = 50;
        int? requested = null;
        view.VolumeChangeRequested += percent => requested = percent;

        view.MouseEvent(new MouseEvent { X = 20, Y = 0, Flags = MouseFlags.WheeledUp });
        Assert.That(requested, Is.EqualTo(50 + VolumeBarView.StepPercent));

        view.MouseEvent(new MouseEvent { X = 20, Y = 0, Flags = MouseFlags.WheeledDown });
        Assert.That(requested, Is.EqualTo(50));
    }

    // ── Клавиатура ────────────────────────────────────────────────────────

    [Test]
    public void CursorKeys_ChangeVolumeAndRaiseEvent()
    {
        var view = CreateView();
        view.Percent = 50;
        int? requested = null;
        view.VolumeChangeRequested += percent => requested = percent;

        view.ProcessKey(new KeyEvent(Key.CursorRight, default));
        Assert.Multiple(() =>
        {
            Assert.That(view.Percent, Is.EqualTo(55));
            Assert.That(requested, Is.EqualTo(55));
        });

        view.ProcessKey(new KeyEvent(Key.CursorLeft, default));
        Assert.That(view.Percent, Is.EqualTo(50));
    }

    [Test]
    public void HomeAndEnd_SetExtremes()
    {
        var view = CreateView();
        view.Percent = 50;

        view.ProcessKey(new KeyEvent(Key.Home, default));
        Assert.That(view.Percent, Is.Zero);

        view.ProcessKey(new KeyEvent(Key.End, default));
        Assert.That(view.Percent, Is.EqualTo(100));
    }

    // ── Отрисовка ─────────────────────────────────────────────────────────

    [Test]
    public void Redraw_ShowsLabelBarAndPercent()
    {
        var view = CreateView();
        view.Percent = 70;

        string text = Render(view);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Громкость"));
            Assert.That(text, Does.Contain(" 70%"));
            Assert.That(text, Does.Contain("█"));
            Assert.That(text, Does.Contain("◆"));
            Assert.That(text, Does.Contain("─"));
        });
    }

    [Test]
    public void Percent_IsClampedToRange()
    {
        var view = CreateView();

        view.Percent = 150;
        Assert.That(view.Percent, Is.EqualTo(100));

        view.Percent = -20;
        Assert.That(view.Percent, Is.Zero);
    }
}
