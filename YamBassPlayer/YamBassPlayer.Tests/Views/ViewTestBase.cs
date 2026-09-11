using System.Text;
using Terminal.Gui;

namespace YamBassPlayer.Tests.Views;

/// <summary>
/// Headless-харнесс для тестов вьюх на Terminal.Gui: вывод рендерится в
/// <see cref="FakeDriver"/>, поэтому логику и грубый результат можно проверять без терминала.
/// ВАЖНО: состояние <see cref="Application"/> глобальное — UI-фикстуры не должны идти параллельно.
/// </summary>
[NonParallelizable]
public abstract class ViewTestBase
{
    protected FakeDriver Driver { get; private set; } = null!;

    [SetUp]
    public void UiSetUp()
    {
        Driver = new FakeDriver();
        Application.Init(Driver, null!);
    }

    [TearDown]
    public void UiTearDown()
    {
        Application.Shutdown();
    }

    /// <summary>
    /// Проталкивает отложенные <c>Application.MainLoop.Invoke</c>: вьюхи списков мутируют
    /// состояние через главный цикл, и без «прокачки» изменения не применятся.
    /// </summary>
    protected static void Pump() => Application.MainLoop.MainIteration();

    /// <summary>
    /// Добавляет вьюху в дерево <see cref="Application.Top"/> и раскладывает её, чтобы
    /// <c>Bounds</c>/<c>ColorScheme</c> были корректны при одиночном рендере.
    /// </summary>
    protected T AddToTop<T>(T view) where T : View
    {
        Application.Top.Add(view);
        if (view.ColorScheme is null)
            view.ColorScheme = new ColorScheme();
        Application.Top.LayoutSubviews();
        return view;
    }

    /// <summary>Читает видимый текст из буфера драйвера (не более rows × cols).</summary>
    protected string RenderText(int rows, int cols)
    {
        int maxRows = Math.Min(rows, Driver.Contents.GetLength(0));
        int maxCols = Math.Min(cols, Driver.Contents.GetLength(1));
        var sb = new StringBuilder();
        for (int r = 0; r < maxRows; r++)
        {
            for (int c = 0; c < maxCols; c++)
            {
                int rune = Driver.Contents[r, c, 0];
                sb.Append(rune > 0 ? char.ConvertFromUtf32(rune) : ' ');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
