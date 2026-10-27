using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

/// <summary>
/// Буфер кадра визуализации. Рендерер рисует в теневую сетку, а драйвер получает
/// только изменившиеся клетки — терминал (и ConPTY) самое медленное звено, и
/// отдавать ему по тысяче клеток на кадр, когда поменялось несколько полос, нельзя.
/// Побочный эффект: клетки, которые перестали быть занятыми, тоже приходят в
/// драйвер как пробелы, поэтому «хвостов» от прошлых кадров не остаётся.
/// </summary>
public sealed class SpectrumCanvas : ISpectrumCanvas
{
    private struct Cell
    {
        public int Rune;
        public Attribute Attribute;
    }

    private Cell[] _frame = [];
    private Cell[] _shown = [];
    private int _width;
    private int _height;

    private Attribute _attribute;
    private int _col;
    private int _row;

    public int Width => _width;

    public int Height => _height;

    /// <summary>
    /// Начинает кадр: запоминает размер полосы и цвет пустых клеток.
    /// При смене размера (или после очистки экрана) следующий кадр нужно отдать
    /// целиком — см. параметр <c>full</c> у <see cref="Flush"/>.
    /// </summary>
    public void BeginFrame(int width, int height, Attribute blank)
    {
        if (width <= 0 || height <= 0)
        {
            _width = 0;
            _height = 0;
            return;
        }

        if (_width != width || _height != height)
        {
            _width = width;
            _height = height;
            int size = width * height;
            _frame = new Cell[size];
            _shown = new Cell[size];
        }

        Array.Fill(_frame, new Cell { Rune = ' ', Attribute = blank });
        _attribute = blank;
        _col = 0;
        _row = 0;
    }

    public void SetAttribute(Attribute attribute) => _attribute = attribute;

    public void Move(int col, int row)
    {
        _col = col;
        _row = row;
    }

    public void AddRune(char rune)
    {
        if ((uint)_row < (uint)_height && (uint)_col < (uint)_width)
        {
            _frame[_row * _width + _col] = new Cell { Rune = rune, Attribute = _attribute };
        }

        _col++;
    }

    /// <summary>
    /// Отдаёт драйверу изменившиеся клетки и возвращает их количество.
    /// <paramref name="full"/> — перерисовать всю полосу (первый кадр, смена режима,
    /// изменение размера, очистка экрана Terminal.Gui).
    /// </summary>
    public int Flush(View host, ConsoleDriver driver, bool full)
    {
        if (_width <= 0 || _height <= 0)
        {
            return 0;
        }

        int changed = 0;
        Attribute lastAttribute = default;
        bool lastAttributeSet = false;

        for (int row = 0; row < _height; row++)
        {
            for (int col = 0; col < _width; col++)
            {
                int index = row * _width + col;
                Cell cell = _frame[index];
                if (!full && SameCell(cell, _shown[index]))
                {
                    continue;
                }

                host.Move(col, row);
                if (!lastAttributeSet || !SameAttribute(lastAttribute, cell.Attribute))
                {
                    driver.SetAttribute(cell.Attribute);
                    lastAttribute = cell.Attribute;
                    lastAttributeSet = true;
                }

                driver.AddRune(cell.Rune);
                changed++;
            }
        }

        (_frame, _shown) = (_shown, _frame);
        return changed;
    }

    private static bool SameCell(Cell left, Cell right)
        => left.Rune == right.Rune && SameAttribute(left.Attribute, right.Attribute);

    private static bool SameAttribute(Attribute left, Attribute right)
        => left.Foreground == right.Foreground && left.Background == right.Background;
}
