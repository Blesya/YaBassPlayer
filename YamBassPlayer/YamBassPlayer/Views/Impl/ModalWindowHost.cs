using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Реализация <see cref="IModalWindowHost"/>: временно переносит <see cref="PlayStatusView"/>
/// в модальное окно, запускает цикл и возвращает панель исходному родителю.
/// </summary>
public sealed class ModalWindowHost : IModalWindowHost
{
	private const int StatusPanelBottomOffset = 6;

	private readonly PlayStatusView _playStatusView;

	public ModalWindowHost(PlayStatusView playStatusView)
	{
		_playStatusView = playStatusView;
	}

	public void Show(IModalView view)
	{
		Toplevel window = view.ModalWindow;
		View? originalParent = _playStatusView.SuperView;
		Pos originalY = _playStatusView.Y;
		bool released = false;

		// Панель обязательно нужно забрать из модального окна до его закрытия:
		// Application.Run в конце вызывает End → RunState.Dispose → Toplevel.Dispose,
		// а тот уничтожает все подвьюхи. Оставленная панель исчезает вместе с окном.
		void Release()
		{
			if (released)
				return;

			released = true;
			window.Remove(_playStatusView);
			_playStatusView.Y = originalY;
			originalParent?.Add(_playStatusView);
			originalParent?.SetNeedsDisplay();
		}

		originalParent?.Remove(_playStatusView);
		_playStatusView.Y = Pos.AnchorEnd(StatusPanelBottomOffset);
		window.Add(_playStatusView);
		window.Closed += _ => Release();

		try
		{
			Application.Run(window);
		}
		finally
		{
			Release();
		}
	}
}
