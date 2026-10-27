using Terminal.Gui;
using YamBassPlayer.Spectrum;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class SpectrumRendererTests : ViewTestBase
{
    private const int Width = 40;
    private const int Height = 10;

    private static readonly Terminal.Gui.Attribute Blank = new(Color.White, Color.Black);

    private View CreateHost() => AddToTop(new View { Width = Width, Height = Height });

    private static float[] SampleData(int n)
        => Enumerable.Range(0, n).Select(i => (i % 16) / 16f).ToArray();

    private static ISpectrumRenderer[] AllRenderers() =>
    [
        new BarsRenderer(),
        new OscilloscopeRenderer(),
        new PolarWaveformRenderer(),
        new LissajousScopeRenderer(),
        new WaterfallRenderer(),
        new RingsRenderer(),
        new Tunnel3DRenderer(),
        new StereoPanScopeRenderer(),
    ];

    /// <summary>Полотно кадра: рендерер рисует в него, а драйвер получает разницу.</summary>
    private static SpectrumCanvas NewCanvas()
    {
        var canvas = new SpectrumCanvas();
        canvas.BeginFrame(Width, Height, Blank);
        return canvas;
    }

    /// <summary>Рисует кадр и отдаёт его драйверу целиком (как при полной перерисовке).</summary>
    private void RenderInto(ISpectrumRenderer renderer, View host, SpectrumCanvas canvas, float[] data)
    {
        renderer.Render(new Rect(0, 0, Width, Height), canvas, data, 22050);
        canvas.Flush(host, Driver, full: true);
    }

    // ── Все рендереры рисуют без исключений ───────────────────────────────

    [Test]
    public void AllRenderers_RenderWithoutThrowing()
    {
        var host = CreateHost();
        var data = SampleData(128);

        Assert.Multiple(() =>
        {
            foreach (var renderer in AllRenderers())
            {
                Assert.That(
                    () => RenderInto(renderer, host, NewCanvas(), data),
                    Throws.Nothing,
                    renderer.DisplayName);
            }
        });
    }

    [Test]
    public void AllRenderers_RenderWithoutThrowingOnEmptyData()
    {
        var host = CreateHost();

        Assert.Multiple(() =>
        {
            foreach (var renderer in AllRenderers())
            {
                Assert.That(
                    () => RenderInto(renderer, host, NewCanvas(), []),
                    Throws.Nothing,
                    renderer.DisplayName);
            }
        });
    }

    [Test]
    public void AllRenderers_HaveDisplayNameAndDefinedDataType()
    {
        foreach (var renderer in AllRenderers())
        {
            Assert.Multiple(() =>
            {
                Assert.That(renderer.DisplayName, Is.Not.Empty, renderer.GetType().Name);
                Assert.That(Enum.IsDefined(renderer.DataType), Is.True, renderer.GetType().Name);
            });
        }
    }

    [Test]
    public void BarsRenderer_Reset_AllowsRenderingAgain()
    {
        var renderer = new BarsRenderer();
        var host = CreateHost();

        RenderInto(renderer, host, NewCanvas(), SampleData(128));
        renderer.Reset();

        Assert.DoesNotThrow(() => RenderInto(renderer, host, NewCanvas(), SampleData(128)));
    }

    [Test]
    public void BarsRenderer_FillsTheWholeWidthRegardlessOfBarCount()
    {
        var renderer = new BarsRenderer();
        var host = CreateHost();
        var data = Enumerable.Repeat(1f, 128).ToArray();

        RenderInto(renderer, host, NewCanvas(), data);

        string bottomRow = RenderText(Height, Width).Split(Environment.NewLine)[Height - 1];
        Assert.Multiple(() =>
        {
            Assert.That(bottomRow[0], Is.EqualTo('█'));
            Assert.That(bottomRow[Width - 1], Is.EqualTo('█'), "полосы должны доходить до правого края");
        });
    }

    // ── Полотно: в драйвер уходят только изменившиеся клетки ──────────────

    [Test]
    public void SpectrumCanvas_FlushSendsOnlyChangedCells()
    {
        var host = CreateHost();
        var canvas = NewCanvas();

        // Первый кадр всегда полный: теневой сетки ещё нет.
        Assert.That(canvas.Flush(host, Driver, full: true), Is.EqualTo(Width * Height));

        canvas.BeginFrame(Width, Height, Blank);
        canvas.Move(0, 0);
        canvas.AddRune('A');
        int added = canvas.Flush(host, Driver, full: false);

        canvas.BeginFrame(Width, Height, Blank);
        canvas.Move(0, 0);
        canvas.AddRune('A');
        int same = canvas.Flush(host, Driver, full: false);

        canvas.BeginFrame(Width, Height, Blank);
        canvas.Move(1, 1);
        canvas.AddRune('B');
        int moved = canvas.Flush(host, Driver, full: false);

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.EqualTo(1), "новая клетка рисуется одна");
            Assert.That(same, Is.EqualTo(0), "повтор того же кадра ничего не отправляет");
            Assert.That(moved, Is.EqualTo(2), "старая клетка стирается, новая рисуется");
        });
    }

    [Test]
    public void SpectrumCanvas_FullFlushSendsEveryCell()
    {
        var host = CreateHost();
        var canvas = NewCanvas();

        int first = canvas.Flush(host, Driver, full: true);

        canvas.BeginFrame(Width, Height, Blank);
        int samePicture = canvas.Flush(host, Driver, full: true);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(Width * Height));
            Assert.That(samePicture, Is.EqualTo(Width * Height),
                "полная отрисовка отправляет все клетки, даже если картинка не изменилась");
        });
    }

    // ── SpectrumView: переключение режимов и рендер ───────────────────────

    [Test]
    public void SpectrumView_CycleMode_WrapsAround()
    {
        var view = AddToTop(new SpectrumView() { Width = Width, Height = Height });
        view.AddRenderer(new BarsRenderer());
        view.AddRenderer(new OscilloscopeRenderer());

        Assert.That(view.ModeCount, Is.EqualTo(2));

        view.CycleMode();
        Assert.That(view.CurrentModeIndex, Is.EqualTo(1));

        view.CycleMode();
        Assert.That(view.CurrentModeIndex, Is.EqualTo(0));
    }

    [Test]
    public void SpectrumView_SelectMode_WrapsNegativeIndex()
    {
        var view = AddToTop(new SpectrumView() { Width = Width, Height = Height });
        view.AddRenderer(new BarsRenderer());
        view.AddRenderer(new OscilloscopeRenderer());

        view.SelectMode(-1);

        Assert.That(view.CurrentModeIndex, Is.EqualTo(1));
    }

    [Test]
    public void SpectrumView_SetDataThenRedraw_DoesNotThrow()
    {
        var view = AddToTop(new SpectrumView() { Width = Width, Height = Height });
        view.AddRenderer(new BarsRenderer());

        view.SetData(SampleData(128));

        Assert.DoesNotThrow(() => view.Redraw(new Rect(0, 0, Width, Height)));
    }

    [Test]
    public void SpectrumView_DrawFrame_SkipsSilenceAfterFirstFrame()
    {
        var view = AddToTop(new SpectrumView() { Width = Width, Height = Height });
        view.AddRenderer(new BarsRenderer());
        var silence = new float[128];

        bool first = view.DrawFrame(silence);
        bool second = view.DrawFrame(silence);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True, "первый тихий кадр гасит оставшиеся полосы");
            Assert.That(second, Is.False, "дальше в тишине перерисовывать нечего");
        });
    }
}
