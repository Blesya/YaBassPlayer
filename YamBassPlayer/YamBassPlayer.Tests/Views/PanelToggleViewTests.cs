using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class PanelToggleViewTests : ViewTestBase
{
    private const int Width = PanelToggleView.ToggleWidth;

    private PanelToggleView CreateView(bool expanded = true, PanelSide side = PanelSide.Right)
        => AddToTop(new PanelToggleView { X = 0, Y = 0, Expanded = expanded, Side = side });

    private string Render(PanelToggleView view)
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
    public void Redraw_RightPanelExpanded_LooksLikeButtonWithRightArrows()
    {
        var view = CreateView(expanded: true, side: PanelSide.Right);

        Assert.That(Render(view), Is.EqualTo("[»»]" + Environment.NewLine));
    }

    [Test]
    public void Redraw_RightPanelCollapsed_LooksLikeButtonWithLeftArrows()
    {
        var view = CreateView(expanded: false, side: PanelSide.Right);

        Assert.That(Render(view), Is.EqualTo("[««]" + Environment.NewLine));
    }

    [Test]
    public void Redraw_LeftPanelExpanded_LooksLikeButtonWithLeftArrows()
    {
        // Дерево плейлистов слева: сворачиваясь, панель уезжает влево.
        var view = CreateView(expanded: true, side: PanelSide.Left);

        Assert.That(Render(view), Is.EqualTo("[««]" + Environment.NewLine));
    }

    [Test]
    public void Redraw_LeftPanelCollapsed_LooksLikeButtonWithRightArrows()
    {
        var view = CreateView(expanded: false, side: PanelSide.Left);

        Assert.That(Render(view), Is.EqualTo("[»»]" + Environment.NewLine));
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
