using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Views.Impl;

public sealed class MyWaveView : Window, IMyWaveView
{
	private readonly CoverAsciiView _asciiView;
	private readonly Label _artistTitleLabel;
	private readonly Label _albumLabel;
	private readonly Label _separatorLabel;
	private readonly Label _waveDescriptionLabel;
	private readonly Label _nextTrackLabel;

	private (int Width, int Height) _appliedCoverSize;
	private bool _applyingLayout;

	public event Action? CoverSizeChanged;

	public Toplevel ModalWindow => this;

	public MyWaveView() : base("Моя волна")
	{
		// Окно занимает весь экран и запускается через Application.Run. Без Modal
		// Terminal.Gui делает его Application.Top, и таймер спектра главного окна
		// продолжает рисовать полосу поверх него.
		Modal = true;

		X = 0;
		Y = 0;
		Width = Dim.Fill();
		Height = Dim.Fill();

		_asciiView = new CoverAsciiView
		{
			X = 1,
			Y = 1
		};

		_artistTitleLabel = new Label
		{
			Width = Dim.Fill(2),
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "- - -"
		};

		_albumLabel = new Label
		{
			Width = Dim.Fill(2),
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = string.Empty
		};

		_separatorLabel = new Label
		{
			Width = Dim.Fill(2),
			Height = 1,
			AutoSize = false,
			Text = new string('─', 200)
		};

		_waveDescriptionLabel = new Label
		{
			Width = Dim.Fill(2),
			Height = 2,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "Моя волна"
		};

		_nextTrackLabel = new Label
		{
			Width = Dim.Fill(2),
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = string.Empty
		};

		var closeButton = new Button
		{
			X = Pos.Center() - 8,
			Y = Pos.AnchorEnd(2),
			Text = "Закрыть [ESC]",
			CanFocus = false
		};
		closeButton.Clicked += Close;

		Add(_asciiView,
			_artistTitleLabel,
			_albumLabel,
			_separatorLabel,
			_waveDescriptionLabel,
			_nextTrackLabel,
			closeButton);

		_appliedCoverSize = ComputeCoverSize();
		ApplyCoverLayout(_appliedCoverSize);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				Close();
				e.Handled = true;
			}
		};
	}

	/// <summary>
	/// Размер обложки по текущему размеру окна. До первой раскладки берётся
	/// размер драйвера: окно полноэкранное, поэтому значения совпадают.
	/// </summary>
	public (int Width, int Height) CoverSize => ComputeCoverSize();

	private (int Width, int Height) ComputeCoverSize()
	{
		int frameWidth = Bounds.Width > 0 ? Bounds.Width : Application.Driver?.Cols ?? 0;
		int frameHeight = Bounds.Height > 0 ? Bounds.Height : Application.Driver?.Rows ?? 0;

		if (frameWidth <= 0 || frameHeight <= 0)
			return (20, 11);

		// Рамка окна, поля и общая панель управления внизу: ModalWindowHost
		// добавляет её в окно и занимает 6 строк.
		return CoverLayout.Compute(
			Math.Max(1, frameWidth - 4),
			Math.Max(1, frameHeight - 9));
	}

	public override void LayoutSubviews()
	{
		base.LayoutSubviews();

		if (_applyingLayout)
			return;

		_applyingLayout = true;
		try
		{
			(int Width, int Height) size = ComputeCoverSize();
			if (size == _appliedCoverSize)
				return;

			_appliedCoverSize = size;
			ApplyCoverLayout(size);
			CoverSizeChanged?.Invoke();
		}
		finally
		{
			_applyingLayout = false;
		}
	}

	private void ApplyCoverLayout((int Width, int Height) size)
	{
		_asciiView.Width = size.Width;
		_asciiView.Height = size.Height;

		int textX = size.Width + 4;
		int frameHeight = Bounds.Height > 0 ? Bounds.Height : Application.Driver?.Rows ?? 0;
		int bottomLimit = Math.Max(0, frameHeight - 3);

		_artistTitleLabel.X = textX;
		_artistTitleLabel.Y = 2;

		_albumLabel.X = textX;
		_albumLabel.Y = Math.Min(5, bottomLimit);

		_separatorLabel.X = textX;
		_separatorLabel.Y = Math.Min(8, bottomLimit);

		_waveDescriptionLabel.X = textX;
		_waveDescriptionLabel.Y = Math.Min(10, bottomLimit);

		_nextTrackLabel.X = textX;
		_nextTrackLabel.Y = Math.Min(13, bottomLimit);
	}

	public void SetTrack(Track track)
	{
		Application.MainLoop.Invoke(() =>
		{
			_artistTitleLabel.Text = $"{track.Artist}   -   {track.Title}";
			_albumLabel.Text = string.IsNullOrWhiteSpace(track.Album)
				? string.Empty
				: $"[ {track.Album} ]";
			SetNeedsDisplay();
		});
	}

	public void SetListenCount(int count) { }

	public void SetCover(CoverArt? art)
	{
		Application.MainLoop.Invoke(() =>
		{
			_asciiView.SetPixels(art);
			SetNeedsDisplay();
		});
	}

	public void SetWaveDescription(string description)
	{
		Application.MainLoop.Invoke(() =>
		{
			_waveDescriptionLabel.Text = $"{description}";
			SetNeedsDisplay();
		});
	}

	public void SetNextTrackLabel(string? label)
	{
		Application.MainLoop.Invoke(() =>
		{
			_nextTrackLabel.Text = string.IsNullOrWhiteSpace(label)
				? string.Empty
				: $"Следующий: {label}";
			SetNeedsDisplay();
		});
	}

	public void Close()
	{
		Application.RequestStop(this);
	}
}
