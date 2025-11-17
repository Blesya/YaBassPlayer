using Terminal.Gui;

namespace YamBassPlayer.Views;

internal sealed class ScrollableTableView : TableView
{
	public event Action? OnScroll;

	public override bool OnMouseEvent(MouseEvent mouseEvent)
	{
		bool result = base.OnMouseEvent(mouseEvent);
		
		if (mouseEvent.Flags.HasFlag(MouseFlags.WheeledDown) ||
		    mouseEvent.Flags.HasFlag(MouseFlags.WheeledUp))
		{
			OnScroll?.Invoke();
		}
		
		return result;
	}
}