using Terminal.Gui;
using YamBassPlayer.Enums;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Компактная кнопка сворачивания боковой панели: скобки как у остальных кнопок
/// интерфейса, внутри — две стрелки. Стрелки смотрят в ту сторону, куда уезжает
/// панель: «« у левой панели, »» у правой, а в свёрнутом виде — обратно, на место
/// панели. Направление совпадает с движением панели в обоих состояниях.
/// </summary>
public sealed class PanelToggleView : View
{
	/// <summary>Ширина кнопки: две скобки и две стрелки внутри.</summary>
	public const int ToggleWidth = 4;

	private const string ArrowsLeft = "««";
	private const string ArrowsRight = "»»";

	private bool _expanded = true;

	public PanelToggleView()
	{
		Width = ToggleWidth;
		Height = 1;

		// Кнопка не участвует в обходе по Tab: фокус должен оставаться на списке
		// треков, а панели переключаются ещё и из меню «Вид».
		CanFocus = false;
	}

	/// <summary>Сторона, на которой находится сворачиваемая панель.</summary>
	public PanelSide Side { get; init; } = PanelSide.Right;

	/// <summary>Панель показана — стрелки смотрят в сторону, куда она уедет.</summary>
	public bool Expanded
	{
		get => _expanded;
		set
		{
			if (_expanded == value)
				return;

			_expanded = value;
			SetNeedsDisplay();
		}
	}

	/// <summary>Клик по кнопке; видимость панели переключает владелец.</summary>
	public event Action? Toggled;

	private bool ArrowsPointLeft
	{
		get
		{
			bool panelOnLeft = Side == PanelSide.Left;
			return Expanded ? panelOnLeft : !panelOnLeft;
		}
	}

	public override bool MouseEvent(MouseEvent me)
	{
		if (!me.Flags.HasFlag(MouseFlags.Button1Clicked))
			return base.MouseEvent(me);

		Toggled?.Invoke();
		return true;
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		if (Bounds.Width <= 0)
			return;

		var driver = Application.Driver;

		// Скобки — обычным цветом, как у остальных кнопок; стрелки — акцентом,
		// чтобы кнопка читалась как нажимаемая, а не как часть текста.
		driver.SetAttribute(ColorScheme.Normal);
		Move(0, 0);
		driver.AddStr("[");
		driver.SetAttribute(ColorScheme.HotNormal);
		driver.AddStr(ArrowsPointLeft ? ArrowsLeft : ArrowsRight);
		driver.SetAttribute(ColorScheme.Normal);
		driver.AddStr("]");
	}
}
