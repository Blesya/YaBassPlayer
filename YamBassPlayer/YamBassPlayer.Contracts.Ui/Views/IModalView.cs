using Terminal.Gui;

namespace YamBassPlayer.Views;

/// <summary>
/// Вьюха, которую показывают как модальное окно. Отдаёт базовый <see cref="Toplevel"/>,
/// чтобы хост мог добавить/убрать общую панель управления, не зная о конкретной вьюхе.
/// </summary>
public interface IModalView
{
	/// <summary>Окно для показа (<c>Application.Run</c>); обычно <c>this</c>.</summary>
	Toplevel ModalWindow { get; }
}
