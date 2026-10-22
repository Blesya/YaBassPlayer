using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// OAuth Device Flow Яндекс.Музыки: приложение просит пару кодов, пользователь вводит
/// короткий код на странице Яндекса, приложение опрашивает токен. В отличие от QR-входа
/// опирается только на документированные эндпоинты OAuth и возвращает refresh-токен.
/// </summary>
public interface IDeviceAuthorizationService
{
	/// <summary>
	/// Запрашивает коды для входа. <c>null</c>, если OAuth недоступен или ответил ошибкой.
	/// </summary>
	Task<DeviceCodeChallenge?> RequestCodeAsync(CancellationToken ct = default);

	/// <summary>Одна проверка, подтвердил ли пользователь код.</summary>
	Task<DeviceAuthResult> PollAsync(string deviceCode, CancellationToken ct = default);

	/// <summary>Меняет refresh-токен на новую пару токенов.</summary>
	Task<DeviceAuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
}
