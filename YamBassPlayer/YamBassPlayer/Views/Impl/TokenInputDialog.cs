using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

public class TokenInputDialog : Dialog
{
	private readonly TextField _tokenField;
	    
	public string? Token { get; private set; }
	public bool Cancelled { get; private set; } = true;

	public TokenInputDialog() : base("Авторизация Яндекс.Музыка")
	{
	    Width = 60;
	    Height = 12;

	    var infoLabel = new Label
	    {
	        Text = "Токен не найден. Укажите токен для Яндекс.Музыки:",
	        X = 1,
	        Y = 1,
	        Width = Dim.Fill(1)
	    };

	    _tokenField = new TextField
	    {
	        X = 1,
	        Y = 4,
	        Width = Dim.Fill(1),
	        Secret = false
	    };

	    var okButton = new Button("OK")
	    {
	        X = Pos.Center() - 10,
	        Y = 6
	    };
	    okButton.Clicked += OnOkClicked;

	    var cancelButton = new Button("Отмена")
	    {
	        X = Pos.Center() + 2,
	        Y = 6
	    };
	    cancelButton.Clicked += OnCancelClicked;

	    Add(infoLabel, _tokenField, okButton, cancelButton);

	    _tokenField.SetFocus();
	}

	private void OnOkClicked()
	{
	    string token = _tokenField.Text?.ToString() ?? string.Empty;
	        
	    if (string.IsNullOrWhiteSpace(token))
	    {
	        MessageBox.ErrorQuery("Ошибка", "Токен не может быть пустым", "OK");
	        return;
	    }

	    Token = token;
	    Cancelled = false;
	    Application.RequestStop();
	}

	private void OnCancelClicked()
	{
	    Cancelled = true;
	    Application.RequestStop();
	}
}