namespace YamBassPlayer.Constants;

public class AuthConst
{
	/// <summary>
	/// client_id официального клиента Яндекс.Музыки. Используется для OAuth Device Flow:
	/// своё приложение с правами на Музыку создать нельзя.
	/// </summary>
	public const string DeviceClientId = "23cabbbdc6cd418abb4b39c32c41195d";

	/// <summary>
	/// Секрет того же клиента. Не секрет в прямом смысле: лежит в APK приложения,
	/// но без него OAuth не выдаёт токен по Device Flow.
	/// </summary>
	public const string DeviceClientSecret = "53bc75238f0c4d08a118e51fe9203300";

	/// <summary>Хост OAuth Яндекса — на нём живут эндпоинты Device Flow.</summary>
	public const string OAuthBaseUrl = "https://oauth.yandex.ru/";

	/// <summary>Страница, на которой пользователь вводит код.</summary>
	public const string DeviceVerificationUrl = "https://ya.ru/device";

	/// <summary>Имя устройства в списке выданных токенов Яндекса.</summary>
	public const string DeviceName = "YamBassPlayer";
}
