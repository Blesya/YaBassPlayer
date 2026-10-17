namespace YamBassPlayer.Presenters;

/// <summary>
/// Проводит пользователя через стартовое окно входа (QR-код или токен)
/// и сообщает, удалось ли авторизоваться.
/// </summary>
public interface IAuthPresenter
{
	/// <summary>Признак успешного входа.</summary>
	bool Authorized { get; }

	/// <summary>Показывает окно входа и возвращает <c>true</c>, если вход выполнен.</summary>
	bool Run();
}
