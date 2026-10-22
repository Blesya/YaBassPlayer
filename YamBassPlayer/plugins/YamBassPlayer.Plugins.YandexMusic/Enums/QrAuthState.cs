namespace YamBassPlayer.Enums;

/// <summary>Результат одной проверки подтверждения входа по QR-коду.</summary>
public enum QrAuthState
{
	/// <summary>Пользователь ещё не подтвердил вход — нужно проверить снова.</summary>
	Pending,

	/// <summary>Вход подтверждён, токен получен.</summary>
	Authorized,

	/// <summary>Проверка не удалась (сеть, истёкший код и т. п.).</summary>
	Error
}
