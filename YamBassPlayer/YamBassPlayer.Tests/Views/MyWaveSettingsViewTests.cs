using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

/// <summary>
/// Окно настроек «Моей волны»: три настройки станции, кнопки
/// «Запустить» / «Отменить» и отмена по Esc.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class MyWaveSettingsViewTests : ViewTestBase
{
    private static KeyEvent Press(Key key) => new(key, new KeyModifiers());

    private MyWaveSettingsView CreateView() => AddToTop(new MyWaveSettingsView());

    /// <summary>Контролы диалога лежат внутри ContentView, а не прямо в Subviews.</summary>
    private static IEnumerable<View> Descendants(View view) =>
        view.Subviews.SelectMany(child => Descendants(child).Prepend(child));

    private static Button FindButton(MyWaveSettingsView view, string text) =>
        Descendants(view).OfType<Button>().Single(button => button.Text?.ToString() == text);

    private static RadioGroup GroupWith(MyWaveSettingsView view, string label) =>
        Descendants(view).OfType<RadioGroup>()
            .Single(group => group.RadioLabels.Any(radioLabel => radioLabel.ToString() == label));

    [Test]
    public void SetSettings_SelectsSavedOptions()
    {
        var view = CreateView();

        view.SetSettings(new MyWaveSettings("discover", "calm", "russian"));

        Assert.Multiple(() =>
        {
            Assert.That(GroupWith(view, "Незнакомое").SelectedItem, Is.EqualTo(1));
            Assert.That(GroupWith(view, "Спокойное").SelectedItem, Is.EqualTo(2));
            Assert.That(GroupWith(view, "Русский").SelectedItem, Is.EqualTo(0));
        });
    }

    [Test]
    public void SelectedSettings_ReturnsChosenOptions()
    {
        var view = CreateView();
        view.SetSettings(MyWaveSettings.Default);

        GroupWith(view, "Популярное").SelectedItem = 2;
        GroupWith(view, "Грустное").SelectedItem = 3;
        GroupWith(view, "Без слов").SelectedItem = 3;

        Assert.That(view.SelectedSettings,
            Is.EqualTo(new MyWaveSettings("popular", "sad", "without-words")));
    }

    /// <summary>В настройках плагина могло остаться чужое или устаревшее значение.</summary>
    [Test]
    public void SetSettings_WithUnknownValues_FallsBackToDefaults()
    {
        var view = CreateView();

        view.SetSettings(new MyWaveSettings("unknown", "unknown", "unknown"));

        Assert.That(view.SelectedSettings, Is.EqualTo(MyWaveSettings.Default));
    }

    [Test]
    public void StartButton_RaisesOnStartClicked()
    {
        var view = CreateView();
        bool started = false;
        view.OnStartClicked += () => started = true;

        FindButton(view, "Запустить").OnClicked();

        Assert.That(started, Is.True);
    }

    [Test]
    public void CancelButton_RaisesOnCancelClicked()
    {
        var view = CreateView();
        bool cancelled = false;
        view.OnCancelClicked += () => cancelled = true;

        FindButton(view, "Отменить").OnClicked();

        Assert.That(cancelled, Is.True);
    }

    [Test]
    public void Enter_StartsWaveFromDefaultButton()
    {
        var view = CreateView();
        bool started = false;
        view.OnStartClicked += () => started = true;

        view.ProcessKey(Press(Key.Enter));
        view.ProcessColdKey(Press(Key.Enter));

        Assert.That(started, Is.True);
    }

    [Test]
    public void Render_ShowsRussianSettingsAndButtons()
    {
        var view = CreateView();
        view.SetSettings(MyWaveSettings.Default);

        view.Redraw(new Rect(0, 0, 72, 20));
        string text = RenderText(20, 72);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Треки"));
            Assert.That(text, Does.Contain("Настроение"));
            Assert.That(text, Does.Contain("Язык"));
            Assert.That(text, Does.Contain("Любимое"));
            Assert.That(text, Does.Contain("Спокойное"));
            Assert.That(text, Does.Contain("Русский"));
            Assert.That(text, Does.Contain("Запустить"));
            Assert.That(text, Does.Contain("Отменить"));
        });
    }
}
