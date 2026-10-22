namespace YamBassPlayer.Views;

/// <summary>
/// Показывает модальные окна, на время показа «одалживая» общую панель
/// <c>PlayStatusView</c> и возвращая её на место после закрытия.
/// </summary>
public interface IModalWindowHost
{
	void Show(IModalView view);
}
