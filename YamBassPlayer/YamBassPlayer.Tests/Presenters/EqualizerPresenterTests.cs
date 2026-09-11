using Moq;
using YamBassPlayer.Configuration;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class EqualizerPresenterTests
{
    private Mock<IBassEqualizer> _bassEqualizer = null!;
    private Mock<IAppConfiguration> _configuration = null!;
    private Mock<IViewFactory> _viewFactory = null!;
    private Mock<IEqualizerView> _view = null!;

    [SetUp]
    public void SetUp()
    {
        _bassEqualizer = new Mock<IBassEqualizer>();
        _configuration = new Mock<IAppConfiguration>();
        _viewFactory = new Mock<IViewFactory>();
        _view = new Mock<IEqualizerView>();

        _viewFactory.Setup(f => f.Create<IEqualizerView>()).Returns(_view.Object);
        _configuration.Setup(c => c.GetEqualizerBands()).Returns(Bands(0f));
    }

    private static float[] Bands(float value)
    {
        var bands = new float[10];
        Array.Fill(bands, value);
        return bands;
    }

    private EqualizerPresenter CreatePresenter()
        => new(_bassEqualizer.Object, _configuration.Object, _viewFactory.Object);

    // ── Загрузка значений из конфига ──────────────────────────────────────

    [Test]
    public void Constructor_AppliesStoredBandsToBass()
    {
        var bands = new float[10];
        for (int i = 0; i < 10; i++)
            bands[i] = i;
        _configuration.Setup(c => c.GetEqualizerBands()).Returns(bands);

        _ = CreatePresenter();

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            _bassEqualizer.Verify(e => e.SetBand(index, bands[index]), Times.Once);
        }
    }

    // ── Диалог ────────────────────────────────────────────────────────────

    [Test]
    public void ShowEqualizerDialog_SetsStoredValuesOnViewAndShowsIt()
    {
        var bands = Bands(1.5f);
        _configuration.Setup(c => c.GetEqualizerBands()).Returns(bands);
        var presenter = CreatePresenter();

        presenter.ShowEqualizerDialog();

        Assert.Multiple(() =>
        {
            _viewFactory.Verify(f => f.Create<IEqualizerView>(), Times.Once);
            _view.Verify(v => v.Show(), Times.Once);
        });

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            _view.Verify(v => v.SetBandValue(index, bands[index]), Times.Once);
        }
    }

    [Test]
    public void BandChanged_AppliesTemporaryValueToBass()
    {
        var presenter = CreatePresenter();
        presenter.ShowEqualizerDialog();

        _view.Raise(v => v.OnBandChanged += null, 3, 7.5f);

        _bassEqualizer.Verify(e => e.SetBand(3, 7.5f), Times.Once);
    }

    // ── OK сохраняет, Cancel откатывает ───────────────────────────────────

    [Test]
    public void OkClicked_PersistsTemporaryValuesToConfig()
    {
        var presenter = CreatePresenter();
        presenter.ShowEqualizerDialog();
        _view.Raise(v => v.OnBandChanged += null, 3, 7.5f);

        float[]? saved = null;
        _configuration
            .Setup(c => c.SaveEqualizerBands(It.IsAny<float[]>()))
            .Callback<float[]>(b => saved = b.ToArray());

        _view.Raise(v => v.OnOkClicked += null);

        Assert.That(saved, Is.Not.Null);
        Assert.That(saved![3], Is.EqualTo(7.5f));
    }

    [Test]
    public void CancelClicked_RevertsBassToSavedValues()
    {
        _configuration.Setup(c => c.GetEqualizerBands()).Returns(Bands(1f));
        var calls = new List<(int Index, float Value)>();
        _bassEqualizer
            .Setup(e => e.SetBand(It.IsAny<int>(), It.IsAny<float>()))
            .Callback<int, float>((i, v) => calls.Add((i, v)));

        var presenter = CreatePresenter();
        presenter.ShowEqualizerDialog();
        _view.Raise(v => v.OnBandChanged += null, 2, 9f);

        _view.Raise(v => v.OnCancelClicked += null);

        Assert.That(calls.Last(c => c.Index == 2).Value, Is.EqualTo(1f));
    }
}
