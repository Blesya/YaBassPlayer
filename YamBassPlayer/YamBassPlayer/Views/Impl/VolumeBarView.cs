using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Регулятор громкости: подпись, кликабельная дорожка и текущее значение в процентах.
/// Клик по дорожке выставляет громкость в точку попадания, колесо и стрелки меняют
/// её шагом, Home/End — крайние значения.
/// </summary>
public sealed class VolumeBarView : View
{
	private const string Label = "Громкость";
	private const char TrackCharacter = '─';
	private const char FillCharacter = '█';
	private const char ThumbCharacter = '◆';

	/// <summary>Шаг изменения стрелками и колесом, процентов.</summary>
	public const int StepPercent = 5;

	/// <summary>Ширина текста значения: «100%».</summary>
	private const int PercentWidth = 4;

	private int _percent = 100;

	public VolumeBarView()
	{
		CanFocus = true;
		Height = 1;
		Width = Dim.Fill();
	}

	/// <summary>Громкость в диапазоне 0..100.</summary>
	public int Percent
	{
		get => _percent;
		set
		{
			int clamped = Math.Clamp(value, 0, 100);
			if (clamped == _percent)
				return;

			_percent = clamped;
			SetNeedsDisplay();
		}
	}

	/// <summary>Запрос изменения громкости, процент 0..100.</summary>
	public event Action<int>? VolumeChangeRequested;

	/// <summary>
	/// Границы дорожки внутри строки: подпись слева, значение справа,
	/// между ними — сама дорожка. Клики по подписи и значению громкость не меняют.
	/// </summary>
	public (int Start, int Width) BarGeometry(int width)
	{
		int start = Label.Length + 1;
		int barWidth = width - start - (PercentWidth + 1);
		return barWidth < 1 ? (start, 0) : (start, barWidth);
	}

	/// <summary>
	/// Преобразует колонку клика в процент 0..100. Крайние колонки соответствуют
	/// началу и концу дорожки — та же формула, что и при отрисовке головки.
	/// </summary>
	public static int PercentForColumn(int column, int barWidth)
	{
		if (barWidth <= 1)
			return 0;

		int clampedColumn = Math.Clamp(column, 0, barWidth - 1);
		int percent = (int)Math.Round(clampedColumn * 100.0 / (barWidth - 1), MidpointRounding.AwayFromZero);
		return Math.Clamp(percent, 0, 100);
	}

	public override bool ProcessKey(KeyEvent kb)
	{
		switch (kb.Key)
		{
			case Key.CursorLeft:
			case Key.CursorDown:
				ChangeBy(-StepPercent);
				return true;
			case Key.CursorRight:
			case Key.CursorUp:
				ChangeBy(StepPercent);
				return true;
			case Key.Home:
				SetVolume(0);
				return true;
			case Key.End:
				SetVolume(100);
				return true;
			default:
				return base.ProcessKey(kb);
		}
	}

	public override bool MouseEvent(MouseEvent me)
	{
		if (me.Flags.HasFlag(MouseFlags.WheeledUp))
		{
			ChangeBy(StepPercent);
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.WheeledDown))
		{
			ChangeBy(-StepPercent);
			return true;
		}

		if (!me.Flags.HasFlag(MouseFlags.Button1Clicked) || Bounds.Width <= 0)
			return base.MouseEvent(me);

		(int start, int barWidth) = BarGeometry(Bounds.Width);
		int column = me.X - start;
		if (barWidth <= 1 || column < 0 || column >= barWidth)
		{
			// Клик по подписи или значению — громкость не меняется.
			return true;
		}

		SetVolume(PercentForColumn(column, barWidth));
		return true;
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		int width = Bounds.Width;
		if (width <= 0)
			return;

		var driver = Application.Driver;
		Terminal.Gui.Attribute highlight = HasFocus ? ColorScheme.Focus : ColorScheme.HotNormal;

		driver.SetAttribute(HasFocus ? ColorScheme.Focus : Themes.DimFor(ColorScheme));
		for (int i = 0; i < Label.Length && i < width; i++)
			AddRune(i, 0, Label[i]);

		(int start, int barWidth) = BarGeometry(width);
		if (barWidth > 0)
		{
			int thumbColumn = barWidth == 1
				? 0
				: (int)Math.Round(_percent / 100.0 * (barWidth - 1), MidpointRounding.AwayFromZero);

			for (int x = 0; x < barWidth; x++)
			{
				driver.SetAttribute(x < thumbColumn ? highlight : Themes.DimFor(ColorScheme));
				AddRune(start + x, 0, x < thumbColumn ? FillCharacter : TrackCharacter);
			}

			driver.SetAttribute(highlight);
			AddRune(start + thumbColumn, 0, ThumbCharacter);
		}

		string percentText = $"{_percent,3}%";
		int percentColumn = width - percentText.Length;
		if (percentColumn > Label.Length)
		{
			driver.SetAttribute(highlight);
			Move(percentColumn, 0);
			driver.AddStr(percentText);
		}
	}

	private void ChangeBy(int delta) => SetVolume(_percent + delta);

	private void SetVolume(int percent)
	{
		Percent = percent;
		VolumeChangeRequested?.Invoke(_percent);
	}
}
