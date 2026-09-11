using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Полоса перемотки воспроизведения: тонкая дорожка, заполненная прошедшая часть и
/// «головка» в текущей позиции. Цвета берутся из активной темы, а клик по любой колонке
/// перематывает трек ровно в то место, куда попал указатель.
/// </summary>
public sealed class SeekBarView : View
{
	private const char TrackCharacter = '─';
	private const char FillCharacter = '█';
	private const char ThumbCharacter = '◆';

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

	/// <summary>Запрос перемотки в позицию, заданную процентом 0..100.</summary>
	public event Action<int>? SeekRequested;

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

	public override bool MouseEvent(MouseEvent me)
	{
		if (me.Flags.HasFlag(MouseFlags.Button1Clicked) && Bounds.Width > 0)
		{
			int percent = PercentForColumn(me.X, Bounds.Width);

			// Обновляем позицию сразу, не дожидаясь следующего тика таймера.
			_fraction = percent / 100f;
			SetNeedsDisplay();
			SeekRequested?.Invoke(percent);
			return true;
		}

		return base.MouseEvent(me);
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		int width = Bounds.Width;
		if (width <= 0)
			return;

		var driver = Application.Driver;
		int thumbColumn = width == 1
			? 0
			: (int)Math.Round(_fraction * (width - 1), MidpointRounding.AwayFromZero);

		driver.SetAttribute(GetNormalColor());
		for (int x = 0; x < width; x++)
		{
			AddRune(x, 0, TrackCharacter);
		}

		if (thumbColumn > 0)
		{
			driver.SetAttribute(GetHotNormalColor());
			for (int x = 0; x < thumbColumn; x++)
			{
				AddRune(x, 0, FillCharacter);
			}
		}

		driver.SetAttribute(GetHotNormalColor());
		AddRune(thumbColumn, 0, ThumbCharacter);
	}
}
