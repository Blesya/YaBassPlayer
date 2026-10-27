using System.Diagnostics;
using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Spectrum;

namespace YamBassPlayer.Views.Impl;

public sealed class SpectrumView : View
{
    /// <summary>Бюджет одного кадра визуализации, мс (совпадает с периодом таймера).</summary>
    private const double FrameBudgetMs = 16;

    private readonly List<ISpectrumRenderer> _renderers = new();
    private readonly SpectrumCanvas _canvas = new();
    private int _currentIndex;
    private ISpectrumRenderer CurrentRenderer => _renderers[_currentIndex];
    private float[] _data = [];

    private bool _fullFrame = true;
    private bool _skipNextTick;
    private bool _lastSilent;
    private double _lastFrameMs;

    public int MaxFrequencyHz { get; set; } = 22050;
    public SpectrumDataType RequiredDataType => CurrentRenderer.DataType;
    public string ModeDisplayName => CurrentRenderer.DisplayName;
    public int ModeCount => _renderers.Count;
    public int CurrentModeIndex => _currentIndex;

    public SpectrumView()
    {
        CanFocus = false;
    }

    public void AddRenderer(ISpectrumRenderer renderer)
    {
        _renderers.Add(renderer);
    }

    /// <summary>Цвет пустых клеток полосы — фон текущей темы.</summary>
    private Terminal.Gui.Attribute BlankAttribute => ColorScheme?.Normal ?? default;

    /// <summary>
    /// Данные для ближайшей перерисовки через механизм Terminal.Gui. Для кадров
    /// визуализации используется <see cref="DrawFrame"/>: он не инвалидирует окно.
    /// </summary>
    public void SetData(float[] data)
    {
        _data = data;
        _lastSilent = false;
        SetNeedsDisplay();
    }

    /// <summary>
    /// Рисует кадр напрямую в драйвер, минуя инвалидацию Terminal.Gui, и возвращает
    /// <c>true</c>, если в терминал действительно что-то ушло (тогда вызывающий
    /// должен обновить экран через <c>Driver.Refresh</c>).
    /// В Terminal.Gui 1.x любая инвалидация дочерней вьюхи заставляет Toplevel
    /// залить весь экран и перерисовать все панели — на 60 кадрах в секунду это
    /// и обложка, и дерево, и список по десять раз в секунду.
    /// </summary>
    public bool DrawFrame(float[] data)
    {
        if (!Visible || _renderers.Count == 0)
        {
            return false;
        }

        Rect bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        // Тишина: кадр не изменится, а рендерер всё равно считал бы FFT-раскладку.
        // Один «тихий» кадр всё же рисуем — он гасит полосы, оставшиеся от звука.
        bool silent = IsSilent(data);
        if (silent && _lastSilent)
        {
            return false;
        }

        if (_skipNextTick)
        {
            _skipNextTick = false;
            return false;
        }

        _lastSilent = silent;
        _data = data;

        ConsoleDriver driver = Application.Driver;
        Rect savedClip = SetClip(bounds);
        long started = Stopwatch.GetTimestamp();

        _canvas.BeginFrame(bounds.Width, bounds.Height, BlankAttribute);
        CurrentRenderer.Render(bounds, _canvas, _data, MaxFrequencyHz);
        int changed = _canvas.Flush(this, driver, _fullFrame);

        _lastFrameMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        driver.Clip = savedClip;
        _fullFrame = false;

        // Кадр не влез в бюджет — следующий пропускаем, чтобы не копить отставание.
        _skipNextTick = _lastFrameMs > FrameBudgetMs;
        return changed > 0;
    }

    public void CycleMode()
    {
        if (_renderers.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _renderers.Count;
        CurrentRenderer.Reset();
        _fullFrame = true;
    }

    public void SelectMode(int index)
    {
        if (_renderers.Count == 0) return;
        _currentIndex = ((index % _renderers.Count) + _renderers.Count) % _renderers.Count;
        CurrentRenderer.Reset();
        _fullFrame = true;
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);

        if (_renderers.Count == 0)
            return;

        ConsoleDriver driver = Application.Driver;
        Rect savedClip = SetClip(bounds);
        _canvas.BeginFrame(bounds.Width, bounds.Height, BlankAttribute);
        CurrentRenderer.Render(bounds, _canvas, _data, MaxFrequencyHz);
        // Полную отрисовку делает Terminal.Gui: экран перед этим мог быть очищен.
        _canvas.Flush(this, driver, full: true);
        driver.Clip = savedClip;
        _fullFrame = false;
    }

    private static bool IsSilent(float[] data)
    {
        foreach (float value in data)
        {
            if (value != 0f)
            {
                return false;
            }
        }

        return true;
    }
}
