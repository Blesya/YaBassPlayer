using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Реализация <see cref="IModalWindowHost"/>: временно переносит <see cref="PlayStatusView"/>
/// в модальное окно, запускает цикл и в <c>finally</c> возвращает панель исходному родителю.
/// </summary>
public sealed class ModalWindowHost : IModalWindowHost
{
	private const int StatusPanelBottomOffset = 5;

	private readonly PlayStatusView _playStatusView;

	public ModalWindowHost(PlayStatusView playStatusView)
	{
		_playStatusView = playStatusView;
	}

	public void Show(IModalView view)
	{
		Toplevel window = view.ModalWindow;
		View? originalParent = _playStatusView.SuperView;

		originalParent?.Remove(_playStatusView);
		_playStatusView.Y = Pos.AnchorEnd(StatusPanelBottomOffset);
		window.Add(_playStatusView);

		try
		{
			Application.Run(window);
		}
		finally
		{
			window.Remove(_playStatusView);
			_playStatusView.Y = Pos.AnchorEnd(StatusPanelBottomOffset);
			originalParent?.Add(_playStatusView);
			originalParent?.SetNeedsDisplay();
		}
	}
}
