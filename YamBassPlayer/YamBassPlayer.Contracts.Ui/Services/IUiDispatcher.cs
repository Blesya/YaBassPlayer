namespace YamBassPlayer.Services;

/// <summary>
/// Маршалинг действий в UI-поток и повторяющиеся таймеры. Абстрагирует статический
/// <c>Application.MainLoop</c>, чтобы презентеры не зависели от Terminal.Gui напрямую
/// и могли тестироваться с синхронной реализацией.
/// </summary>
public interface IUiDispatcher
{
	/// <summary>Выполняет действие в UI-потоке.</summary>
	void Invoke(Action action);

	/// <summary>Запускает повторяющийся таймер; колбэк возвращает <c>false</c>, чтобы остановить его.</summary>
	void StartTimer(TimeSpan interval, Func<bool> onTick);
}
