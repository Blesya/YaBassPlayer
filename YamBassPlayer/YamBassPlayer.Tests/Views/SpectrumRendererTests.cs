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

    private View CreateHost() => AddToTop(new View { Width = Width, Height = Height });

    private static float[] SampleData(int n)
        => Enumerable.Range(0, n).Select(i => (i % 16) / 16f).ToArray();

    private static ISpectrumRenderer[] AllRenderers() =>
    [
        new BarsRenderer(10),
        new OscilloscopeRenderer(),
        new PolarWaveformRenderer(),
        new LissajousScopeRenderer(),
        new WaterfallRenderer(),
        new RingsRenderer(),
        new Tunnel3DRenderer(),
        new StereoPanScopeRenderer(),
    ];

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
                    () => renderer.Render(new Rect(0, 0, Width, Height), Driver, host, data, 22050),
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
                    () => renderer.Render(new Rect(0, 0, Width, Height), Driver, host, [], 22050),
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
        var renderer = new BarsRenderer(10);
        var host = CreateHost();

        renderer.Render(new Rect(0, 0, Width, Height), Driver, host, SampleData(128), 22050);
        renderer.Reset();

        Assert.DoesNotThrow(() => renderer.Render(new Rect(0, 0, Width, Height), Driver, host, SampleData(128), 22050));
    }

    // ── SpectrumView: переключение режимов и рендер ───────────────────────

    [Test]
    public void SpectrumView_CycleMode_WrapsAround()
    {
        var view = AddToTop(new SpectrumView(10) { Width = Width, Height = Height });
        view.AddRenderer(new BarsRenderer(10));
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
        var view = AddToTop(new SpectrumView(10) { Width = Width, Height = Height });
        view.AddRenderer(new BarsRenderer(10));
        view.AddRenderer(new OscilloscopeRenderer());

        view.SelectMode(-1);

        Assert.That(view.CurrentModeIndex, Is.EqualTo(1));
    }

    [Test]
    public void SpectrumView_SetDataThenRedraw_DoesNotThrow()
    {
        var view = AddToTop(new SpectrumView(10) { Width = Width, Height = Height });
        view.AddRenderer(new BarsRenderer(10));

        view.SetData(SampleData(128));

        Assert.DoesNotThrow(() => view.Redraw(new Rect(0, 0, Width, Height)));
    }
}
