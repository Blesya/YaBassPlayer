using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

/// <summary>
/// «Моя волна» из меню: выбор в окне запоминается, применяется к станции
/// и только потом запускает волну через хост.
/// </summary>
[TestFixture]
public sealed class MyWaveSettingsPresenterTests
{
    private static readonly MyWaveSettings Chosen = new("discover", "calm", "russian");

    private Mock<IYandexRadioService> _radio = null!;
    private Mock<IMyWaveService> _myWave = null!;
    private Mock<IMyWaveSettingsView> _view = null!;
    private Mock<IErrorHandler> _errors = null!;
    private FakePluginSettings _settings = null!;
    private MyWaveSettingsPresenter _presenter = null!;

    [SetUp]
    public void SetUp()
    {
        _radio = new Mock<IYandexRadioService>();
        _radio.Setup(r => r.ApplyMyWaveSettingsAsync(It.IsAny<MyWaveSettings>())).ReturnsAsync(true);

        _myWave = new Mock<IMyWaveService>();
        _myWave.Setup(s => s.ShowAsync(It.IsAny<Action<string>>())).Returns(Task.CompletedTask);

        _view = new Mock<IMyWaveSettingsView>();
        _errors = new Mock<IErrorHandler>();
        _settings = new FakePluginSettings();

        _presenter = CreatePresenter();
    }

    private MyWaveSettingsPresenter CreatePresenter(IUiDispatcher? dispatcher = null)
    {
        var ui = new Mock<IPluginUi>();
        ui.SetupGet(u => u.Dispatcher).Returns(dispatcher ?? new FakeUiDispatcher());

        return new MyWaveSettingsPresenter(
            _radio.Object,
            _myWave.Object,
            ui.Object,
            _settings,
            _errors.Object,
            () => _view.Object);
    }

    private void CloseWithCancel() =>
        _view.Setup(v => v.Show()).Callback(() => _view.Raise(v => v.OnCancelClicked += null));

    private void CloseWithStart() =>
        _view.Setup(v => v.Show()).Callback(() => _view.Raise(v => v.OnStartClicked += null));

    // ── Окно ──────────────────────────────────────────────────────────────

    [Test]
    public async Task ShowAsync_OpensViewWithSavedSettings()
    {
        _settings.Set("myWaveDiversity", "popular");
        _settings.Set("myWaveMoodEnergy", "sad");
        _settings.Set("myWaveLanguage", "russian");
        CloseWithCancel();

        await _presenter.ShowAsync();

        _view.Verify(v => v.SetSettings(new MyWaveSettings("popular", "sad", "russian")), Times.Once);
        _view.Verify(v => v.Show(), Times.Once);
    }

    [Test]
    public async Task ShowAsync_WithStaleSavedValue_OpensViewWithDefaults()
    {
        _settings.Set("myWaveDiversity", "bogus");
        CloseWithCancel();

        await _presenter.ShowAsync();

        _view.Verify(v => v.SetSettings(MyWaveSettings.Default), Times.Once);
    }

    /// <summary>
    /// Пункт меню выполняется в фоновой задаче, а Application.Run обязан идти
    /// в UI-потоке — иначе драйвер Terminal.Gui крутится из двух потоков сразу.
    /// </summary>
    [Test]
    public async Task ShowAsync_CreatesAndShowsViewOnUiDispatcher()
    {
        var dispatcher = new RecordingUiDispatcher();
        bool createdInDispatcher = false;
        bool shownInDispatcher = false;

        _view.Setup(v => v.Show()).Callback(() =>
        {
            shownInDispatcher = dispatcher.IsInvoking;
            _view.Raise(v => v.OnCancelClicked += null);
        });

        var presenter = new MyWaveSettingsPresenter(
            _radio.Object,
            _myWave.Object,
            UiWith(dispatcher),
            _settings,
            _errors.Object,
            () =>
            {
                createdInDispatcher = dispatcher.IsInvoking;
                return _view.Object;
            });

        await presenter.ShowAsync();

        Assert.Multiple(() =>
        {
            Assert.That(createdInDispatcher, Is.True, "окно должно создаваться в UI-потоке");
            Assert.That(shownInDispatcher, Is.True, "Application.Run должен запускаться в UI-потоке");
        });
    }

    // ── Отмена ────────────────────────────────────────────────────────────

    [Test]
    public async Task Cancel_DoesNotApplySettingsAndDoesNotStartWave()
    {
        CloseWithCancel();

        await _presenter.ShowAsync();

        Assert.Multiple(() =>
        {
            _radio.Verify(r => r.ApplyMyWaveSettingsAsync(It.IsAny<MyWaveSettings>()), Times.Never);
            _myWave.Verify(s => s.ShowAsync(It.IsAny<Action<string>>()), Times.Never);
            Assert.That(_settings.Get("myWaveDiversity"), Is.Null);
        });
    }

    /// <summary>
    /// Esc обрабатывает сам Dialog: окно закрывается, событие отмены не приходит.
    /// Закрытие без «Запустить» тоже должно означать отмену.
    /// </summary>
    [Test]
    public async Task DialogClosedWithoutStart_DoesNotApplySettingsAndDoesNotStartWave()
    {
        _view.Setup(v => v.Show()); // окно закрылось само

        await _presenter.ShowAsync();

        Assert.Multiple(() =>
        {
            _radio.Verify(r => r.ApplyMyWaveSettingsAsync(It.IsAny<MyWaveSettings>()), Times.Never);
            _myWave.Verify(s => s.ShowAsync(It.IsAny<Action<string>>()), Times.Never);
        });
    }

    // ── Запуск ────────────────────────────────────────────────────────────

    [Test]
    public async Task Start_SavesAppliesAndStartsWave()
    {
        _view.Setup(v => v.SelectedSettings).Returns(Chosen);
        CloseWithStart();

        await _presenter.ShowAsync();

        Assert.Multiple(() =>
        {
            _radio.Verify(r => r.ApplyMyWaveSettingsAsync(Chosen), Times.Once);
            _myWave.Verify(s => s.ShowAsync(It.IsAny<Action<string>>()), Times.Once);
            Assert.That(_settings.Get("myWaveDiversity"), Is.EqualTo("discover"));
            Assert.That(_settings.Get("myWaveMoodEnergy"), Is.EqualTo("calm"));
            Assert.That(_settings.Get("myWaveLanguage"), Is.EqualTo("russian"));
        });
    }

    /// <summary>Сбой настроек не должен мешать запуску: станция стартует с прежними.</summary>
    [Test]
    public async Task Start_WhenSettingsRejected_StillStartsWave()
    {
        _radio.Setup(r => r.ApplyMyWaveSettingsAsync(It.IsAny<MyWaveSettings>())).ReturnsAsync(false);
        _view.Setup(v => v.SelectedSettings).Returns(Chosen);
        CloseWithStart();

        await _presenter.ShowAsync();

        _myWave.Verify(s => s.ShowAsync(It.IsAny<Action<string>>()), Times.Once);
        _errors.Verify(e => e.Handle(It.IsAny<Exception>()), Times.Never);
    }

    [Test]
    public async Task Start_WhenWaveFails_ReportsError()
    {
        _view.Setup(v => v.SelectedSettings).Returns(Chosen);
        _myWave.Setup(s => s.ShowAsync(It.IsAny<Action<string>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        CloseWithStart();

        await _presenter.ShowAsync();

        _errors.Verify(e => e.Handle(It.Is<Exception>(ex => ex.Message == "boom")), Times.Once);
    }

    [Test]
    public async Task ShowAsync_WhenViewThrows_ReportsError()
    {
        var presenter = new MyWaveSettingsPresenter(
            _radio.Object,
            _myWave.Object,
            UiWith(new FakeUiDispatcher()),
            _settings,
            _errors.Object,
            () => throw new InvalidOperationException("boom"));

        await presenter.ShowAsync();

        _errors.Verify(e => e.Handle(It.Is<Exception>(ex => ex.Message == "boom")), Times.Once);
    }

    private static IPluginUi UiWith(IUiDispatcher dispatcher)
    {
        var ui = new Mock<IPluginUi>();
        ui.SetupGet(u => u.Dispatcher).Returns(dispatcher);
        return ui.Object;
    }

    private sealed class FakePluginSettings : IPluginSettings
    {
        private readonly Dictionary<string, string?> _values = new();

        public string? Get(string key) => _values.TryGetValue(key, out string? value) ? value : null;

        public void Set(string key, string? value) => _values[key] = value;
    }

    /// <summary>Диспетчер, запоминающий, выполняется ли действие прямо сейчас.</summary>
    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public bool IsInvoking { get; private set; }

        public void Invoke(Action action)
        {
            IsInvoking = true;

            try
            {
                action();
            }
            finally
            {
                IsInvoking = false;
            }
        }

        public void StartTimer(TimeSpan interval, Func<bool> onTick)
        {
        }
    }
}
