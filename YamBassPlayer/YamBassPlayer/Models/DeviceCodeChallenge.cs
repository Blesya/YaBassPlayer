namespace YamBassPlayer.Models;

/// <summary>
/// Пара кодов, которую OAuth выдаёт для входа по Device Flow: <c>DeviceCode</c>
/// остаётся в приложении, <c>UserCode</c> показывается пользователю.
/// </summary>
public sealed record DeviceCodeChallenge(
	string DeviceCode,
	string UserCode,
	string VerificationUrl,
	TimeSpan Interval,
	TimeSpan Lifetime);
