using YamBassPlayer.Enums;

namespace YamBassPlayer.Models;

/// <summary>
/// Ответ OAuth на опрос подтверждения или на обмен refresh-токена.
/// Токены заполнены только при <see cref="DeviceAuthState.Authorized"/>.
/// </summary>
public sealed record DeviceAuthResult(
	DeviceAuthState State,
	string? AccessToken,
	string? RefreshToken,
	TimeSpan? Lifetime)
{
	public bool IsAuthorized => State == DeviceAuthState.Authorized;
}
