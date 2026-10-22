using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

public sealed class ErrorDialog : Dialog
{
    private readonly TextView _textView;

    public ErrorDialog(string title, string text) : base(title, 80, 24)
    {
        _textView = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill(4),
            ReadOnly = true,
            WordWrap = true,
            CanFocus = true
        };
        _textView.Text = text;

        var closeButton = new Button("OK", is_default: true);
        closeButton.Clicked += () => Application.RequestStop(this);

        AddButton(closeButton);
        Add(_textView);

        KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.Esc)
            {
                Application.RequestStop(this);
                e.Handled = true;
            }
        };
    }

    public static void Show(string title, string text)
    {
        var dialog = new ErrorDialog(title, text);
        Application.Run(dialog);
    }
}
