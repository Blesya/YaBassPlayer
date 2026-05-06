using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Двухстрочная панель-«мини-консоль» под панелью управления воспроизведением.
/// Первая строка выводит результат выполнения последней команды,
/// вторая — поле ввода команд.
/// </summary>
public sealed class CommandInputView : View, ICommandInputView
{
	private readonly Label _resultLabel;
	private readonly TextField _input;

	public event Action<string>? OnCommandSubmitted;

	public CommandInputView()
	{
		Width = Dim.Fill();
		Height = 2;

		_resultLabel = new Label
		{
			X = 1,
			Y = 0,
			Width = Dim.Fill(1),
			Height = 1,
			Text = "Готов к приёму команд. Введите help"
		};

		_input = new TextField
		{
			X = 0,
			Y = 1,
			Width = Dim.Fill(),
			Height = 1
		};
		_input.KeyPress += OnInputKeyPress;

		Add(_resultLabel, _input);
	}

	private void OnInputKeyPress(KeyEventEventArgs e)
	{
		if (e.KeyEvent.Key != Key.Enter)
			return;

		string text = _input.Text?.ToString() ?? string.Empty;
		_input.Text = string.Empty;
		OnCommandSubmitted?.Invoke(text.Trim());
		e.Handled = true;
	}

	public void FocusInput() => _input.SetFocus();

	public void SetResult(string result)
	{
		Application.MainLoop.Invoke(() =>
		{
			_resultLabel.Text = string.IsNullOrWhiteSpace(result) ? " " : result;
		});
	}
}
