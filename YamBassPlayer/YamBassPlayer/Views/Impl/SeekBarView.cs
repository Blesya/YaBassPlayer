using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Полоса перемотки воспроизведения: прошедшее время слева, оставшееся справа,
/// между ними — дорожка, заполненная прошедшая часть и «головка» в текущей позиции.
/// Цвета берутся из активной темы, служебные элементы приглушены, а клик по дорожке
/// перематывает трек ровно в то место, куда попал указатель.
/// </summary>
public sealed class SeekBarView : View
{
	private const char TrackCharacter = '─';
	private const char FillCharacter = '█';
	private const char ThumbCharacter = '◆';

	/// <summary>Минимальная ширина дорожки: если она не помещается, время не показывается.</summary>
	private const int MinBarWidth = 10;

	private float _fraction;

	public SeekBarView()
	{
		CanFocus = false;
		Height = 1;
		Width = Dim.Fill();
	}

	/// <summary>Позиция воспроизведения в диапазоне 0..1.</summary>
	public float Fraction
	{
		get => _fraction;
		set
		{
			float clamped = Math.Clamp(value, 0f, 1f);
			if (Math.Abs(clamped - _fraction) < 0.001f)
				return;

			_fraction = clamped;
			SetNeedsDisplay();
		}
	}

	/// <summary>Позиция воспроизведения.</summary>
	public TimeSpan Current { get; private set; }

	/// <summary>Длительность трека; <see cref="TimeSpan.Zero"/> — время не показывается.</summary>
	public TimeSpan Duration { get; private set; }

	/// <summary>Показывает прошедшее и оставшееся время по краям дорожки.</summary>
	public bool ShowsTime => Duration > TimeSpan.Zero;

	/// <summary>Запрос перемотки в позицию, заданную процентом 0..100.</summary>
	public event Action<int>? SeekRequested;

	/// <summary>Задаёт позицию и длительность; процент пересчитывается из них.</summary>
	public void SetTime(TimeSpan current, TimeSpan duration)
	{
		if (current == Current && duration == Duration)
			return;

		Current = current < TimeSpan.Zero ? TimeSpan.Zero : current;
		Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
		Fraction = Duration > TimeSpan.Zero ? (float)(Current.TotalSeconds / Duration.TotalSeconds) : 0f;
		SetNeedsDisplay();
	}

	/// <summary>
	/// Преобразует колонку клика в процент 0..100. Крайние колонки соответствуют началу
	/// и концу дорожки — та же формула, что и при отрисовке головки, поэтому позиция
	/// совпадает с местом клика, а не «уезжает» влево из-за округления.
	/// </summary>
	public static int PercentForColumn(int column, int width)
	{
		if (width <= 1)
			return 0;

		int clampedColumn = Math.Clamp(column, 0, width - 1);
		int percent = (int)Math.Round(clampedColumn * 100.0 / (width - 1), MidpointRounding.AwayFromZero);
		return Math.Clamp(percent, 0, 100);
	}

	/// <summary>
	/// Границы дорожки внутри строки: время слева и справа занимает свои колонки,
	/// а клики по ним перемоткой не считаются.
	/// </summary>
	public (int Start, int Width) BarGeometry(int width)
	{
		if (!ShowsTime)
			return (0, width);

		int elapsed = Format(Current).Length;
		int total = Format(Duration).Length;
		if (width < elapsed + total + MinBarWidth + 2)
			return (0, width);

		return (elapsed + 1, width - elapsed - total - 2);
	}

	public override bool MouseEvent(MouseEvent me)
	{
		if (!me.Flags.HasFlag(MouseFlags.Button1Clicked) || Bounds.Width <= 0)
			return base.MouseEvent(me);

		(int start, int barWidth) = BarGeometry(Bounds.Width);
		int column = me.X - start;
		if (barWidth <= 1 || column < 0 || column >= barWidth)
		{
			// Клик по времени (или дорожка не помещается) — перемотки нет.
			return true;
		}

		int percent = PercentForColumn(column, barWidth);

		// Обновляем позицию сразу, не дожидаясь следующего тика таймера.
		_fraction = percent / 100f;
		SetNeedsDisplay();
		SeekRequested?.Invoke(percent);
		return true;
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		int width = Bounds.Width;
		if (width <= 0)
			return;

		var driver = Application.Driver;
		(int start, int barWidth) = BarGeometry(width);
		if (barWidth <= 0)
			return;

		if (start > 0)
		{
			driver.SetAttribute(Themes.DimFor(ColorScheme));
			Move(0, 0);
			driver.AddStr(Format(Current).PadRight(start));
		}

		int thumbColumn = barWidth == 1
			? 0
			: (int)Math.Round(_fraction * (barWidth - 1), MidpointRounding.AwayFromZero);

		for (int x = 0; x < barWidth; x++)
		{
			driver.SetAttribute(x < thumbColumn ? ColorScheme.HotNormal : Themes.DimFor(ColorScheme));
			AddRune(start + x, 0, x < thumbColumn ? FillCharacter : TrackCharacter);
		}

		driver.SetAttribute(ColorScheme.HotNormal);
		AddRune(start + thumbColumn, 0, ThumbCharacter);

		if (start > 0)
		{
			driver.SetAttribute(Themes.DimFor(ColorScheme));
			Move(start + barWidth, 0);
			driver.AddStr(" " + Format(Duration));
		}
	}

	private static string Format(TimeSpan time)
		=> time < TimeSpan.Zero ? "00:00" : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
}
