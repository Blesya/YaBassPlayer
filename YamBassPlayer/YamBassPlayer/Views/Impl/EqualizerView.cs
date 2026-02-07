using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

public sealed class EqualizerView : Dialog, IEqualizerView
{
	private const int MaxValue = 10;
	private const int BarHeight = 21; // -10 to +10 = 21 позиций
	
	private static readonly string[] BandLabels = 
	{
		"32", "64", "125", "250", "500",
		"1k", "2k", "4k", "8k", "16k"
	};

	private readonly int[] _bandValues = new int[10];
	private readonly Label[] _valueLabels = new Label[10];

	public event Action? OnOkClicked;
	public event Action? OnCancelClicked;
	public event Action<int, float>? OnBandChanged;

	public EqualizerView() : base("Эквалайзер", 76, 30)
	{
		int spacing = 7;

		for (int i = 0; i < 10; i++)
		{
			int bandIndex = i;
			int xPos = 2 + i * spacing;

			// Кнопка +
			var upButton = new Button("+")
			{
				X = xPos + 1,
				Y = 1,
				Width = 3,
				Height = 1
			};
			upButton.Clicked += () => AdjustBand(bandIndex, 1);

			// Визуальная полоса
			var barView = new EqualizerBarView(bandIndex, this)
			{
				X = xPos + 2,
				Y = 3,
				Width = 1,
				Height = BarHeight
			};

			// Кнопка -
			var downButton = new Button("-")
			{
				X = xPos + 1,
				Y = 3 + BarHeight,
				Width = 3,
				Height = 1
			};
			downButton.Clicked += () => AdjustBand(bandIndex, -1);

			// Значение
			_valueLabels[i] = new Label
			{
				X = xPos,
				Y = 3 + BarHeight + 1,
				Width = 4,
				Text = "  0"
			};

			// Частота
			var bandLabel = new Label
			{
				X = xPos,
				Y = 3 + BarHeight + 2,
				Width = 5,
				Text = BandLabels[i]
			};

			Add(upButton, barView, downButton, _valueLabels[i], bandLabel);
		}

		var okButton = new Button("ОК");
		okButton.Clicked += () =>
		{
			OnOkClicked?.Invoke();
			Application.RequestStop(this);
		};

		var cancelButton = new Button("Отмена");
		cancelButton.Clicked += () =>
		{
			OnCancelClicked?.Invoke();
			Application.RequestStop(this);
		};

		AddButton(okButton);
		AddButton(cancelButton);
	}

	private void AdjustBand(int bandIndex, int delta)
	{
		int newValue = _bandValues[bandIndex] + delta;
		if (newValue < -MaxValue) newValue = -MaxValue;
		if (newValue > MaxValue) newValue = MaxValue;

		_bandValues[bandIndex] = newValue;
		UpdateValueLabel(bandIndex);
		SetNeedsDisplay();

		float normalizedValue = newValue / (float)MaxValue;
		OnBandChanged?.Invoke(bandIndex, normalizedValue);
	}

	private void UpdateValueLabel(int bandIndex)
	{
		int value = _bandValues[bandIndex];
		_valueLabels[bandIndex].Text = value >= 0 ? $"+{value,2}" : $"{value,3}";
	}

	public int GetBandValueInt(int bandIndex) => _bandValues[bandIndex];

	public void SetBandValueDirect(int bandIndex, int value)
	{
		if (bandIndex < 0 || bandIndex >= 10)
			return;

		_bandValues[bandIndex] = value;
		UpdateValueLabel(bandIndex);
		SetNeedsDisplay();

		float normalizedValue = value / (float)MaxValue;
		OnBandChanged?.Invoke(bandIndex, normalizedValue);
	}

	public void SetBandValue(int bandIndex, float value)
	{
		if (bandIndex < 0 || bandIndex >= 10)
			return;

		_bandValues[bandIndex] = (int)(value * MaxValue);
		UpdateValueLabel(bandIndex);
		SetNeedsDisplay();
	}

	public float[] GetBandValues()
	{
		float[] values = new float[10];
		for (int i = 0; i < 10; i++)
		{
			values[i] = _bandValues[i] / (float)MaxValue;
		}
		return values;
	}

	public void Show()
	{
		Application.Run(this);
	}

	public void Close()
	{
		Application.RequestStop(this);
	}

	// Внутренний класс для отрисовки полосы
	private class EqualizerBarView : View
	{
		private readonly int _bandIndex;
		private readonly EqualizerView _parent;

		public EqualizerBarView(int bandIndex, EqualizerView parent)
		{
			_bandIndex = bandIndex;
			_parent = parent;
			
			// Включаем обработку кликов мыши
			CanFocus = true;
		}

		public override bool MouseEvent(MouseEvent mouseEvent)
		{
			if (mouseEvent.Flags.HasFlag(MouseFlags.Button1Clicked) ||
				mouseEvent.Flags.HasFlag(MouseFlags.Button1Pressed))
			{
				int clickY = mouseEvent.Y;
				int midY = BarHeight / 2;
				
				// Преобразуем позицию клика в значение
				// y=0 -> +10, y=midY -> 0, y=BarHeight-1 -> -10
				int newValue = midY - clickY;
				
				if (newValue < -MaxValue) newValue = -MaxValue;
				if (newValue > MaxValue) newValue = MaxValue;
				
				_parent.SetBandValueDirect(_bandIndex, newValue);
				return true;
			}
			
			return base.MouseEvent(mouseEvent);
		}

		public override void Redraw(Rect bounds)
		{
			base.Redraw(bounds);
			
			int value = _parent.GetBandValueInt(_bandIndex);
			int midY = BarHeight / 2; // Середина = 0
			
			for (int y = 0; y < BarHeight; y++)
			{
				Move(0, y);
				
				int levelFromTop = MaxValue - y; // +10 сверху, -10 снизу
				bool isMidLine = (y == midY);
				bool isFilled;
				
				if (value > 0)
				{
					// Заполняем от середины вверх
					isFilled = (y >= midY - value && y <= midY);
				}
				else if (value < 0)
				{
					// Заполняем от середины вниз
					isFilled = (y >= midY && y <= midY - value);
				}
				else
				{
					isFilled = isMidLine;
				}

				if (isFilled)
				{
					if (value > 0)
						Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Green, Color.Black));
					else if (value < 0)
						Driver.SetAttribute(new Terminal.Gui.Attribute(Color.Red, Color.Black));
					else
						Driver.SetAttribute(new Terminal.Gui.Attribute(Color.White, Color.Black));
					Driver.AddStr("█");
				}
				else if (isMidLine)
				{
					Driver.SetAttribute(new Terminal.Gui.Attribute(Color.White, Color.Black));
					Driver.AddStr("─");
				}
				else
				{
					Driver.SetAttribute(new Terminal.Gui.Attribute(Color.DarkGray, Color.Black));
					Driver.AddStr("│");
				}
			}
		}
	}
}
