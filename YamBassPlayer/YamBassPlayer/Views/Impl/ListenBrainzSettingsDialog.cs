using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Настройки ListenBrainz: имя пользователя и токен. Значения сохраняются в
/// <c>appsettings.json</c> и применяются сразу, без перезапуска плеера.
/// </summary>
public sealed class ListenBrainzSettingsDialog : Dialog
{
	private readonly TextField _userField;
	private readonly TextField _tokenField;

	public ListenBrainzSettingsDialog(string? userName, string? token)
		: base("Настройки", 64, 8)
	{
		Add(new Label { X = 1, Y = 0, Text = "Имя" });
		_userField = new TextField(userName ?? string.Empty)
		{
			X = 8,
			Y = 0,
			Width = Dim.Fill() - 2
		};
		Add(_userField);

		Add(new Label { X = 1, Y = 1, Text = "Токен" });
		_tokenField = new TextField(token ?? string.Empty)
		{
			X = 8,
			Y = 1,
			Width = Dim.Fill() - 2,
			Secret = true
		};
		Add(_tokenField);

		var okButton = new Button("OK", is_default: true);
		okButton.Clicked += () =>
		{
			Result = (ReadField(_userField), ReadField(_tokenField));
			Application.RequestStop(this);
		};

		var cancelButton = new Button("Отмена");
		cancelButton.Clicked += () => Application.RequestStop(this);

		AddButton(okButton);
		AddButton(cancelButton);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key != Key.Esc)
				return;

			Application.RequestStop(this);
			e.Handled = true;
		};
	}

	/// <summary><see langword="null"/> — окно закрыли без сохранения.</summary>
	public (string UserName, string Token)? Result { get; private set; }

	public static (string UserName, string Token)? Show(string? userName, string? token)
	{
		var dialog = new ListenBrainzSettingsDialog(userName, token);
		Application.Run(dialog);
		return dialog.Result;
	}

	private static string ReadField(TextField field) => (field.Text?.ToString() ?? string.Empty).Trim();
}
