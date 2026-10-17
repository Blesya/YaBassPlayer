namespace YamBassPlayer.Enums;

/// <summary>Способ входа в Яндекс.Музыку, предлагаемый в стартовом окне авторизации.</summary>
public enum AuthMethod
{
	/// <summary>Вход по QR-коду: пользователь сканирует код приложением Яндекс и подтверждает вход.</summary>
	Qr,

	/// <summary>Вход по готовому OAuth-токену, скопированному вручную.</summary>
	Token
}
