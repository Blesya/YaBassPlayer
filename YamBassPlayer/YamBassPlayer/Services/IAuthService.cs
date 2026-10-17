using YamBassPlayer.Enums;
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

	/// <summary>
	/// Открывает сессию входа по QR-коду и возвращает ссылку, которую нужно закодировать в QR.
	/// </summary>
	Task<string> StartQrAuthorizationAsync();

	/// <summary>Одна проверка статуса подтверждения входа по QR-коду.</summary>
	Task<QrAuthState> CheckQrAuthorizationAsync();
}
