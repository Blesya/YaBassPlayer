namespace YamBassPlayer.Enums;

/// <summary>Результат одной проверки подтверждения входа по коду устройства.</summary>
public enum DeviceAuthState
{
	/// <summary>Пользователь ещё не ввёл код — нужно проверить снова.</summary>
	Pending,

	/// <summary>Код подтверждён, токены получены.</summary>
	Authorized,

	/// <summary>Код устарел — нужно запросить новый.</summary>
	Expired,

	/// <summary>Пользователь отказал в доступе.</summary>
	Denied,

	/// <summary>Проверка не удалась (сеть, неизвестный ответ OAuth и т. п.).</summary>
	Error
}
