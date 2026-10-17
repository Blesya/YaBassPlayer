using YamBassPlayer.Enums;

namespace YamBassPlayer.Views;

/// <summary>
/// Стартовое окно авторизации: слева список способов входа, справа — содержимое
/// выбранного способа (QR-код или поле для токена), внизу строка состояния и кнопки.
/// </summary>
public interface IAuthView : IModalView
{
	/// <summary>Пользователь выбрал другой способ входа.</summary>
	event Action<AuthMethod>? OnMethodSelected;

	/// <summary>Пользователь ввёл токен и подтвердил вход.</summary>
	event Action<string>? OnTokenSubmitted;

	/// <summary>Пользователь запросил новый QR-код.</summary>
	event Action? OnQrRefreshRequested;

	/// <summary>Пользователь просит открыть ссылку входа в браузере.</summary>
	event Action<string>? OnOpenLinkRequested;

	/// <summary>Пользователь отказался от входа.</summary>
	event Action? OnCancelRequested;

	/// <summary>Заполняет список способов и показывает первый из них.</summary>
	void SetMethods(IReadOnlyList<AuthMethod> methods);

	/// <summary>Выбирает способ программно (без события <see cref="OnMethodSelected"/>).</summary>
	void SelectMethod(AuthMethod method);

	/// <summary>Показывает в правой панели содержимое выбранного способа.</summary>
	void ShowMethod(AuthMethod method);

	/// <summary>Пишет текст в строку состояния.</summary>
	void SetStatus(string text, bool isError = false);

	/// <summary>Показывает QR-код и ссылку, закодированную в нём.</summary>
	void ShowQrCode(string qrText, string link);

	/// <summary>Блокирует кнопки на время выполнения операции.</summary>
	void SetBusy(bool busy);

	/// <summary>Закрывает окно.</summary>
	void Close();
}
