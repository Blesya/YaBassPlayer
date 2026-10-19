using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Services;

public interface IAuthService
{
	bool IsAuthorized { get; }
	AuthStorage Storage { get; }
	YandexMusicApi Api { get; }

	/// <summary>Авторизация по готовому OAuth-токену.</summary>
	Task<bool> AuthorizeAsync(string token);

	/// <summary>Авторизация по токену из appsettings.json.</summary>
	Task<bool> AuthorizeFromConfigAsync();

	/// <summary>Тихое продление сессии по refresh-токену из конфига.</summary>
	Task<bool> TryRefreshAsync();

	/// <summary>
	/// Открывает сессию входа по QR-коду и возвращает ссылку, которую нужно закодировать в QR.
	/// </summary>
	Task<string> StartQrAuthorizationAsync();

	/// <summary>Одна проверка статуса подтверждения входа по QR-коду.</summary>
	Task<QrAuthState> CheckQrAuthorizationAsync();

	/// <summary>
	/// Запрашивает коды для входа по Device Flow. <c>null</c>, если OAuth недоступен.
	/// </summary>
	Task<DeviceCodeChallenge?> StartDeviceAuthorizationAsync();

	/// <summary>
	/// Одна проверка подтверждения кода устройства. При успехе сервис уже авторизован,
	/// а в результате лежат токены для сохранения.
	/// </summary>
	Task<DeviceAuthResult> CheckDeviceAuthorizationAsync();
}
