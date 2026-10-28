using Terminal.Gui;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

/// <summary>
/// Полноэкранные окна запускаются через <c>Application.Run</c> и обязаны быть
/// модальными: иначе Terminal.Gui делает их <c>Application.Top</c>, и таймер
/// спектра главного окна продолжает рисовать полосу поверх них.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class FullScreenWindowTests : ViewTestBase
{
    [Test]
    public void MyWaveView_IsModal()
    {
        Assert.That(AddToTop(new MyWaveView()).Modal, Is.True);
    }

    [Test]
    public void NowPlayingView_IsModal()
    {
        Assert.That(AddToTop(new NowPlayingView()).Modal, Is.True);
    }

    [Test]
    public void LargeTrackInfoView_IsModal()
    {
        Assert.That(AddToTop(new LargeTrackInfoView()).Modal, Is.True);
    }

    [Test]
    public void MyWaveView_CoverFitsSmallTerminal()
    {
        var view = AddToTop(new MyWaveView());

        var (width, height) = view.CoverSize;

        Assert.Multiple(() =>
        {
            Assert.That(width, Is.InRange(1, 76));
            Assert.That(height, Is.InRange(1, 20));
        });
    }

    [Test]
    public void LargeTrackInfoView_CoverFitsSmallTerminal()
    {
        var view = AddToTop(new LargeTrackInfoView());

        var (width, height) = view.CoverSize;

        Assert.Multiple(() =>
        {
            Assert.That(width, Is.InRange(1, 76));
            Assert.That(height, Is.InRange(1, 20));
        });
    }

    [Test]
    public void MyWaveView_WhenSizeChanges_RaisesCoverSizeChanged()
    {
        var view = AddToTop(new MyWaveView());
        var initial = view.CoverSize;

        bool raised = false;
        view.CoverSizeChanged += () => raised = true;

        view.Frame = new Rect(0, 0, 60, 20);
        view.LayoutSubviews();

        Assert.Multiple(() =>
        {
            Assert.That(view.CoverSize, Is.Not.EqualTo(initial));
            Assert.That(raised, Is.True);
        });
    }

    [Test]
    public void LargeTrackInfoView_WhenSizeChanges_RaisesCoverSizeChanged()
    {
        var view = AddToTop(new LargeTrackInfoView());
        var initial = view.CoverSize;

        bool raised = false;
        view.CoverSizeChanged += () => raised = true;

        view.Frame = new Rect(0, 0, 60, 20);
        view.LayoutSubviews();

        Assert.Multiple(() =>
        {
            Assert.That(view.CoverSize, Is.Not.EqualTo(initial));
            Assert.That(raised, Is.True);
        });
    }
}
