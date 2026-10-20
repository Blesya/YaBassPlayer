using Terminal.Gui;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class InfoPanelToggleViewTests : ViewTestBase
{
    private const int Width = InfoPanelToggleView.ToggleWidth;

    private InfoPanelToggleView CreateView(bool expanded = true)
        => AddToTop(new InfoPanelToggleView { X = 0, Y = 0, Expanded = expanded });

    private string Render(InfoPanelToggleView view)
    {
        view.Redraw(new Rect(0, 0, Width, 1));
        return RenderText(1, Width);
    }

    private static MouseEvent Click()
        => new() { X = 0, Y = 0, Flags = MouseFlags.Button1Clicked };

    [Test]
    public void Bounds_AreFourColumnsWide()
    {
        var view = CreateView();

        Assert.Multiple(() =>
        {
            Assert.That(view.Bounds.Width, Is.EqualTo(4));
            Assert.That(view.Bounds.Height, Is.EqualTo(1));
        });
    }

    [Test]
    public void Redraw_Expanded_LooksLikeButtonWithCollapseArrows()
    {
        var view = CreateView(expanded: true);

        Assert.That(Render(view), Is.EqualTo("[»»]" + Environment.NewLine));
    }

    [Test]
    public void Redraw_Collapsed_LooksLikeButtonWithExpandArrows()
    {
        var view = CreateView(expanded: false);

        Assert.That(Render(view), Is.EqualTo("[««]" + Environment.NewLine));
    }

    [Test]
    public void Expanded_Change_RepaintsArrow()
    {
        var view = CreateView(expanded: true);
        Assert.That(Render(view), Is.EqualTo("[»»]" + Environment.NewLine));

        view.Expanded = false;

        Assert.That(Render(view), Is.EqualTo("[««]" + Environment.NewLine));
    }

    [Test]
    public void Click_RaisesToggledAndIsHandled()
    {
        var view = CreateView();
        int toggled = 0;
        view.Toggled += () => toggled++;

        bool handled = view.MouseEvent(Click());

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(toggled, Is.EqualTo(1));
        });
    }

    [Test]
    public void Click_DoesNotChangeStateByItself()
    {
        // Видимостью панели владеет MainWindow: кнопка только сообщает о клике,
        // иначе стрелка разъехалась бы с реальным состоянием панели.
        var view = CreateView(expanded: true);

        view.MouseEvent(Click());

        Assert.That(view.Expanded, Is.True);
    }

    [Test]
    public void OtherMouseButton_IsNotHandledAndRaisesNothing()
    {
        var view = CreateView();
        int toggled = 0;
        view.Toggled += () => toggled++;

        bool handled = view.MouseEvent(new MouseEvent { X = 0, Y = 0, Flags = MouseFlags.Button2Clicked });

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(toggled, Is.Zero);
        });
    }
}
