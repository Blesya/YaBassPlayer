using Terminal.Gui;
using YamBassPlayer.Enums;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Окно входа: слева список способов, справа — панель выбранного способа.
/// Код для входа на сайте и QR рисуются текстом, токен вводится в скрытом поле.
/// Вся логика — в презентере, вьюха только отображает состояние и поднимает события.
/// </summary>
public sealed class AuthView : Dialog, IAuthView
{
	private const int MethodsWidth = 24;
	private const string QrInstruction = "Отсканируйте код приложением Яндекс или Яндекс.Браузером:";
	private const string DeviceInstruction = "Откройте страницу в браузере и введите код:";

	private readonly ListView _methodsList;
	private readonly FrameView _detailPanel;
	private readonly Label _statusLabel;
	private readonly Button _primaryButton;
	private readonly Button _openLinkButton;
	private readonly Button _copyCodeButton;
	private readonly Button _cancelButton;

	private readonly View _devicePanel;
	private readonly Label _deviceInstructionLabel;
	private readonly Label _deviceCodeLabel;
	private readonly Label _deviceLinkLabel;

	private readonly View _qrPanel;
	private readonly Label _qrInstructionLabel;
	private readonly Label _qrCodeLabel;

	private readonly View _tokenPanel;
	private readonly TextField _tokenField;

	private List<AuthMethod> _methods = [];
	private AuthMethod _current = AuthMethod.Device;
	private string _link = string.Empty;
	private bool _suppressSelection;

	public Toplevel ModalWindow => this;

	public event Action<AuthMethod>? OnMethodSelected;
	public event Action<string>? OnTokenSubmitted;
	public event Action? OnDeviceRefreshRequested;
	public event Action? OnQrRefreshRequested;
	public event Action<string>? OnOpenLinkRequested;
	public event Action? OnCancelRequested;

	public AuthView() : base("Вход в Яндекс.Музыку")
	{
		X = 0;
		Y = 0;
		Width = Dim.Fill();
		Height = Dim.Fill();

		var methodsLabel = new Label
		{
			X = 1,
			Y = 1,
			Text = "Способы входа"
		};

		_methodsList = new ListView
		{
			X = 1,
			Y = 2,
			Width = MethodsWidth,
			Height = Dim.Fill(3),
			AllowsMarking = false
		};
		_methodsList.SelectedItemChanged += HandleMethodChanged;

		_detailPanel = new FrameView
		{
			X = Pos.Right(_methodsList) + 1,
			Y = 1,
			Width = Dim.Fill(2),
			Height = Dim.Fill(3)
		};

		// ── Панель кода на сайте ──────────────────────────────────────
		_deviceInstructionLabel = new Label
		{
			X = 1,
			Y = 1,
			Width = Dim.Fill(2),
			Height = 2,
			Text = DeviceInstruction
		};

		_deviceLinkLabel = new Label
		{
			X = 1,
			Y = 3,
			Width = Dim.Fill(2),
			Height = 1,
			Text = string.Empty
		};

		_deviceCodeLabel = new Label
		{
			X = 1,
			Y = 5,
			Width = Dim.Fill(2),
			Height = 1,
			CanFocus = false,
			TextAlignment = TextAlignment.Centered,
			Text = string.Empty
		};

		_devicePanel = new View
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		_devicePanel.Add(_deviceInstructionLabel, _deviceLinkLabel, _deviceCodeLabel);

		// ── Панель QR-кода ────────────────────────────────────────────
		_qrInstructionLabel = new Label
		{
			X = 1,
			Y = 1,
			Width = Dim.Fill(2),
			Height = 2,
			Text = QrInstruction
		};

		_qrCodeLabel = new Label
		{
			X = 1,
			Y = 3,
			Width = Dim.Fill(2),
			Height = Dim.Fill(1),
			CanFocus = false,
			TextAlignment = TextAlignment.Centered,
			Text = string.Empty
		};

		_qrPanel = new View
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		_qrPanel.Add(_qrInstructionLabel, _qrCodeLabel);

		// ── Панель токена ─────────────────────────────────────────────
		var tokenInstruction = new Label
		{
			X = 1,
			Y = 1,
			Width = Dim.Fill(2),
			Height = 4,
			Text = "Вставьте OAuth-токен Яндекс.Музыки и нажмите «Войти».\n" +
			       "Токен можно получить в разделе «Вход по токену» на music.yandex.ru."
		};

		_tokenField = new TextField
		{
			X = 1,
			Y = 6,
			Width = Dim.Fill(2),
			Secret = true
		};

		_tokenPanel = new View
		{
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		_tokenPanel.Add(tokenInstruction, _tokenField);

		_detailPanel.Add(_devicePanel, _qrPanel, _tokenPanel);

		// ── Строка состояния и кнопки ─────────────────────────────────
		_statusLabel = new Label
		{
			X = 1,
			Y = Pos.AnchorEnd(2),
			Width = Dim.Fill(2),
			Height = 1,
			Text = string.Empty
		};

		_primaryButton = new Button("Новый код")
		{
			X = 1,
			Y = Pos.AnchorEnd(1)
		};
		_primaryButton.Clicked += HandlePrimaryClicked;

		_openLinkButton = new Button("Открыть ссылку")
		{
			X = Pos.Right(_primaryButton) + 1,
			Y = Pos.AnchorEnd(1)
		};
		_openLinkButton.Clicked += () => OnOpenLinkRequested?.Invoke(_link);

		_copyCodeButton = new Button("Копировать код")
		{
			X = Pos.Right(_openLinkButton) + 1,
			Y = Pos.AnchorEnd(1),
			Visible = false
		};
		_copyCodeButton.Clicked += HandleCopyCode;

		_cancelButton = new Button("Отмена")
		{
			X = Pos.AnchorEnd(11),
			Y = Pos.AnchorEnd(1)
		};
		_cancelButton.Clicked += () => OnCancelRequested?.Invoke();

		Add(methodsLabel, _methodsList, _detailPanel, _statusLabel, _primaryButton, _openLinkButton, _copyCodeButton, _cancelButton);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				OnCancelRequested?.Invoke();
				e.Handled = true;
			}
		};
	}

	/// <inheritdoc/>
	public void SetMethods(IReadOnlyList<AuthMethod> methods)
	{
		_methods = methods.ToList();

		_suppressSelection = true;
		_methodsList.SetSource(_methods.Select(DisplayName).ToList());
		_methodsList.SelectedItem = 0;
		_suppressSelection = false;

		if (_methods.Count > 0)
			ShowMethod(_methods[0]);
	}

	/// <inheritdoc/>
	public void SelectMethod(AuthMethod method)
	{
		int index = _methods.IndexOf(method);
		if (index < 0)
			return;

		// Меняем выделение молча: иначе сработает OnMethodSelected и презентер
		// заново запустит поток, от которого мы как раз уходим.
		_suppressSelection = true;
		_methodsList.SelectedItem = index;
		_suppressSelection = false;

		ShowMethod(method);
	}

	/// <inheritdoc/>
	public void ShowMethod(AuthMethod method)
	{
		_current = method;
		bool isDevice = method == AuthMethod.Device;
		bool isQr = method == AuthMethod.Qr;

		_devicePanel.Visible = isDevice;
		_qrPanel.Visible = isQr;
		_tokenPanel.Visible = !isDevice && !isQr;
		_detailPanel.Title = method switch
		{
			AuthMethod.Device => "Код на сайте",
			AuthMethod.Qr => "QR-код",
			_ => "Токен"
		};
		_primaryButton.Text = isDevice || isQr ? "Новый код" : "Войти";
		_openLinkButton.Visible = isDevice || isQr;
		_copyCodeButton.Visible = isDevice;

		SetStatus(string.Empty);

		if (!isDevice && !isQr)
			_tokenField.SetFocus();

		_detailPanel.SetNeedsDisplay();
	}

	/// <inheritdoc/>
	public void SetStatus(string text, bool isError = false)
	{
		// Ошибку подсвечиваем схемой Error, обычные подсказки наследуют цвета диалога.
		_statusLabel.ColorScheme = isError ? Colors.Error : null;
		_statusLabel.Text = text;
		_statusLabel.SetNeedsDisplay();
	}

	/// <inheritdoc/>
	public bool ShowDeviceCode(string userCode, string link)
	{
		_link = link;
		_deviceCodeLabel.Text = userCode;
		_deviceLinkLabel.Text = link;
		_deviceCodeLabel.SetNeedsDisplay();
		_deviceLinkLabel.SetNeedsDisplay();

		// Код короткий, но перепечатывать его в браузер вручную — лишняя работа.
		return TryCopy(userCode);
	}

	/// <inheritdoc/>
	public void ShowQrCode(string qrText, string link)
	{
		_link = link;
		_qrCodeLabel.Text = qrText;
		_qrInstructionLabel.Text = $"{QrInstruction}\n{link}";
		_qrCodeLabel.SetNeedsDisplay();
		_qrInstructionLabel.SetNeedsDisplay();
	}

	/// <inheritdoc/>
	public void SetBusy(bool busy)
	{
		_primaryButton.Enabled = !busy;
		_openLinkButton.Enabled = !busy;
		_copyCodeButton.Enabled = !busy;
	}

	/// <inheritdoc/>
	public void Close() => Application.RequestStop(this);

	private void HandleMethodChanged(ListViewItemEventArgs args)
	{
		if (_suppressSelection)
			return;

		if (args.Item < 0 || args.Item >= _methods.Count)
			return;

		AuthMethod method = _methods[args.Item];
		ShowMethod(method);
		OnMethodSelected?.Invoke(method);
	}

	private void HandlePrimaryClicked()
	{
		switch (_current)
		{
			case AuthMethod.Device:
				OnDeviceRefreshRequested?.Invoke();
				return;
			case AuthMethod.Qr:
				OnQrRefreshRequested?.Invoke();
				return;
		}

		string token = _tokenField.Text?.ToString()?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(token))
		{
			SetStatus("Введите токен.", isError: true);
			return;
		}

		OnTokenSubmitted?.Invoke(token);
	}

	private void HandleCopyCode()
	{
		string code = _deviceCodeLabel.Text?.ToString()?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(code))
			return;

		bool copied = TryCopy(code);
		SetStatus(
			copied ? "Код скопирован в буфер обмена." : "Буфер обмена недоступен — введите код вручную.",
			isError: !copied);
	}

	private static bool TryCopy(string text)
	{
		try
		{
			return Clipboard.TrySetClipboardData(text);
		}
		catch
		{
			return false;
		}
	}

	private static string DisplayName(AuthMethod method) => method switch
	{
		AuthMethod.Device => "Код на сайте",
		AuthMethod.Qr => "QR-код",
		AuthMethod.Token => "Токен",
		_ => method.ToString()
	};
}
