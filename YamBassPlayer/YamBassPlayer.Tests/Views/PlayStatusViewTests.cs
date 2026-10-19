using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class PlayStatusViewTests : ViewTestBase
{
    // FakeDriver в тестах — 80x25, поэтому панель проверяется в ширину, которая
    // в него помещается; правый край проверяется сдвигом панели влево.
    private const int Width = 76;
    private const int Height = 6;
    private const int WideWidth = 140;
    private const int RightShift = -40;

    private PlayStatusView CreateView(int x = 0, int width = Width)
        => AddToTop(new PlayStatusView { X = x, Width = width, Height = Height });

    private string Render(PlayStatusView view, int width = Width)
    {
        view.Redraw(new Rect(0, 0, width, Height));
        return RenderText(Height, width);
    }

    [Test]
    public void Redraw_UsesRussianLabelsInsteadOfEnglish()
    {
        string text = Render(CreateView());

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Играть"));
            Assert.That(text, Does.Contain("Стоп"));
            Assert.That(text, Does.Contain("Заново"));
            Assert.That(text, Does.Contain("Назад"));
            Assert.That(text, Does.Contain("Вперёд"));

            Assert.That(text, Does.Not.Contain("Play/Pause"));
            Assert.That(text, Does.Not.Contain("Stop"));
            Assert.That(text, Does.Not.Contain("Restart"));
            Assert.That(text, Does.Not.Contain("Prev"));
            Assert.That(text, Does.Not.Contain("Next"));
        });
    }

    [Test]
    public void SetPlaying_TogglesPlayAndPauseLabel()
    {
        var view = CreateView();

        view.SetPlaying(true);
        Pump();
        Assert.That(Render(view), Does.Contain("Пауза"));

        view.SetPlaying(false);
        Pump();
        Assert.That(Render(view), Does.Contain("Играть"));
    }

    [Test]
    public void SetTime_ShowsElapsedAndTotalInsideTheSeekBar()
    {
        var view = CreateView();

        view.SetTime(TimeSpan.FromSeconds(83), TimeSpan.FromMinutes(5));
        Pump();

        string text = Render(view);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("01:23"));
            Assert.That(text, Does.Contain("05:00"));
        });
    }

    [Test]
    public void SetVolume_ShowsVolumeBarWithPercent()
    {
        var view = CreateView();

        view.SetVolume(70);
        Pump();

        string text = Render(view);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Громкость"));
            Assert.That(text, Does.Contain(" 70%"));
        });
    }

    [Test]
    public void FavoriteButtons_ShowStateWithoutSymbols()
    {
        // Правый край панели шире экрана — сдвигаем панель влево, чтобы он попал в кадр.
        var view = CreateView(x: RightShift, width: WideWidth);

        view.SetLocalFavoriteState(true);
        view.SetYandexFavoriteState(false);
        Pump();

        string text = Render(view, WideWidth);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Локально +"));
            Assert.That(text, Does.Contain("Яндекс -"));
        });
    }

    [Test]
    public void SetPlaybackMode_ShowsRussianLabel()
    {
        var view = CreateView(x: RightShift, width: WideWidth);

        view.SetPlaybackMode(PlaybackMode.Shuffle);
        Pump();
        Assert.That(Render(view, WideWidth), Does.Contain("Случайно"));

        view.SetPlaybackMode(PlaybackMode.Repeat);
        Pump();
        Assert.That(Render(view, WideWidth), Does.Contain("Повтор"));

        view.SetPlaybackMode(PlaybackMode.Sequential);
        Pump();
        Assert.That(Render(view, WideWidth), Does.Contain("Поочерёдно"));
    }
}
