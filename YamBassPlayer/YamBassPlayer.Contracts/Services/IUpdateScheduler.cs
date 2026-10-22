namespace YamBassPlayer.Services;

/// <summary>
/// Фоновые проверки обновлений: одна после старта приложения и дальше раз в час.
/// </summary>
public interface IUpdateScheduler
{
	/// <summary>Взводит таймеры. Повторный вызов ничего не делает.</summary>
	void Start();
}
