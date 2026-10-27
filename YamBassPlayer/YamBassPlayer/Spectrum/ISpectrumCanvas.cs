using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer.Spectrum;

/// <summary>
/// Полотно, в которое рисует визуализация. Рендереру не нужен драйвер консоли:
/// он пишет клетки в буфер кадра (<see cref="SpectrumCanvas"/>), а в терминал
/// попадают только те клетки, которые действительно изменились.
/// </summary>
public interface ISpectrumCanvas
{
    /// <summary>Цвет последующих клеток.</summary>
    void SetAttribute(Attribute attribute);

    /// <summary>Позиция следующей клетки (координаты внутри полосы).</summary>
    void Move(int col, int row);

    /// <summary>Ставит символ в текущую позицию и сдвигает курсор вправо.</summary>
    void AddRune(char rune);
}
