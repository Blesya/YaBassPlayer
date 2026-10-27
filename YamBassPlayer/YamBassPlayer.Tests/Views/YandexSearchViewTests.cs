using Terminal.Gui;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

/// <summary>
/// Окно поиска по Яндекс.Музыке: ввод запроса и запуск поиска с клавиатуры,
/// без обязательного клика мышью по кнопке «Найти».
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class YandexSearchViewTests : ViewTestBase
{
    private static KeyEvent Press(Key key) => new(key, new KeyModifiers());

    private YandexSearchView CreateView() => AddToTop(new YandexSearchView());

    /// <summary>Поле ввода и кнопки лежат внутри ContentView окна, а не прямо в Subviews.</summary>
    private static IEnumerable<View> Descendants(View view) =>
        view.Subviews.SelectMany(child => Descendants(child).Prepend(child));

    private static TextField SearchField(YandexSearchView view) => Descendants(view).OfType<TextField>().Single();

    private static Button FindButton(YandexSearchView view, string text) =>
        Descendants(view).OfType<Button>().Single(b => b.Text?.ToString() == text);

    // ── Запуск поиска ─────────────────────────────────────────────────────

    [Test]
    public void EnterInSearchField_RaisesSearchWithEnteredQuery()
    {
        var view = CreateView();
        var field = SearchField(view);
        field.Text = "кино";
        Assert.That(field.HasFocus, Is.True, "окно открывается с курсором в поле ввода");

        string? query = null;
        view.OnSearchClicked += q => query = q;

        view.ProcessKey(Press(Key.Enter));

        Assert.That(query, Is.EqualTo("кино"));
    }

    [Test]
    public void SearchButtonClick_RaisesSearchWithEnteredQuery()
    {
        var view = CreateView();
        SearchField(view).Text = "кино";

        string? query = null;
        view.OnSearchClicked += q => query = q;

        FindButton(view, "Найти").OnClicked();

        Assert.That(query, Is.EqualTo("кино"));
    }

    /// <summary>
    /// Enter в поле ввода — это поиск, а не подтверждение диалога:
    /// иначе «Найти» и «OK» неразличимы с клавиатуры.
    /// </summary>
    [Test]
    public void EnterInSearchField_DoesNotConfirmDialog()
    {
        var view = CreateView();
        SearchField(view).Text = "кино";

        bool okClicked = false;
        bool searchClicked = false;
        view.OnOkClicked += () => okClicked = true;
        view.OnSearchClicked += _ => searchClicked = true;

        view.ProcessKey(Press(Key.Enter));

        Assert.Multiple(() =>
        {
            Assert.That(searchClicked, Is.True, "Enter должен запускать поиск");
            Assert.That(okClicked, Is.False, "Enter в поле ввода не должен подтверждать диалог");
        });
    }

    /// <summary>
    /// Вне поля ввода Enter не перехватывается списком результатов и достаётся
    /// кнопке по умолчанию, чтобы отмеченные треки добавлялись без мыши.
    /// </summary>
    [Test]
    public void EnterOutsideSearchField_ConfirmsDialog()
    {
        var view = CreateView();
        var field = SearchField(view);

        // Tab от поля ввода: кнопка «Найти», затем вкладки и список результатов.
        view.FocusNext();
        view.FocusNext();
        view.FocusNext();
        Assert.That(field.HasFocus, Is.False, "фокус должен уйти из поля ввода");

        bool okClicked = false;
        view.OnOkClicked += () => okClicked = true;

        bool handledByView = view.ProcessKey(Press(Key.Enter));
        view.ProcessColdKey(Press(Key.Enter));

        Assert.Multiple(() =>
        {
            Assert.That(handledByView, Is.False, "список результатов не должен съедать Enter");
            Assert.That(okClicked, Is.True);
        });
    }
}
