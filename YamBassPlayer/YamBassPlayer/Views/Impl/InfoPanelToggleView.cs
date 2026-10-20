using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Компактная кнопка панели «Инфо»: скобки как у остальных кнопок интерфейса,
/// внутри — две стрелки. Стрелки »» скрывают панель, «« возвращают её:
/// направление совпадает с тем, куда уезжает панель.
/// </summary>
public sealed class InfoPanelToggleView : View
{
	/// <summary>Ширина кнопки: две скобки и две стрелки внутри.</summary>
	public const int ToggleWidth = 4;

	/// <summary>Стрелки «свернуть»: панель уезжает вправо.</summary>
	public const string CollapseArrows = "»»";

	/// <summary>Стрелки «показать»: панель возвращается слева.</summary>
	public const string ExpandArrows = "««";

	/// <summary>Кнопка целиком в состоянии «панель показана».</summary>
	public static string CollapseGlyph => $"[{CollapseArrows}]";

	/// <summary>Кнопка целиком в состоянии «панель скрыта».</summary>
	public static string ExpandGlyph => $"[{ExpandArrows}]";

	private bool _expanded = true;

	public InfoPanelToggleView()
	{
		Width = ToggleWidth;
		Height = 1;

		// Кнопка не участвует в обходе по Tab: фокус должен оставаться на списке
		// треков, а панель переключается ещё и из меню «Вид».
		CanFocus = false;
	}

	/// <summary>Панель показана — стрелки смотрят вправо (свернуть).</summary>
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
		driver.AddStr(Expanded ? CollapseArrows : ExpandArrows);
		driver.SetAttribute(ColorScheme.Normal);
		driver.AddStr("]");
	}
}
