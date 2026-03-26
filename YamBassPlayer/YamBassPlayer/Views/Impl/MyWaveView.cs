using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class MyWaveView : Window, IMyWaveView
{
	private const int CoverWidth = 100;
	private const int CoverHeight = 55;

	private readonly CoverAsciiView _asciiView;
	private readonly Label _artistTitleLabel;
	private readonly Label _albumLabel;
	private readonly Label _waveDescriptionLabel;
	private readonly Label _nextTrackLabel;

	public Action? OnClose { get; set; }

	public MyWaveView() : base("Моя волна")
	{
		X = 0;
		Y = 0;
		Width = Dim.Fill();
		Height = Dim.Fill();

		_asciiView = new CoverAsciiView
		{
			X = 1,
			Y = 1,
			Width = CoverWidth,
			Height = CoverHeight
		};

		_artistTitleLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 2,
			Width = Dim.Fill() - 2,
			Height = 2,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "— — —"
		};

		_albumLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 5,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = string.Empty
		};

		var separatorLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 8,
			Width = Dim.Fill() - 2,
			Height = 1,
			AutoSize = false,
			Text = new string('─', 200)
		};

		_waveDescriptionLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 10,
			Width = Dim.Fill() - 2,
			Height = 2,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "Моя волна"
		};

		_nextTrackLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 13,
			Width = Dim.Fill() - 2,
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
			separatorLabel,
			_waveDescriptionLabel,
			_nextTrackLabel,
			closeButton);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key == Key.Esc)
			{
				Close();
				e.Handled = true;
			}
		};
	}

	public void SetTrack(Track track)
	{
		Application.MainLoop.Invoke(() =>
		{
			_artistTitleLabel.Text = $"{track.Artist}   —   {track.Title}";
			_albumLabel.Text = string.IsNullOrWhiteSpace(track.Album)
				? string.Empty
				: $"[ {track.Album} ]";
			SetNeedsDisplay();
		});
	}

	public void SetListenCount(int count) { }

	public void SetCover(string? coverPath)
	{
		Application.MainLoop.Invoke(() =>
		{
			if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath))
			{
				_asciiView.SetPixels(null);
				return;
			}

			try
			{
				_asciiView.SetPixels(CoverAsciiView.RenderAscii(coverPath, CoverWidth, CoverHeight));			}
			catch
			{
				_asciiView.SetPixels(null);
			}
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

	public void Show()
	{
		Application.Run(this);
	}

	public void Close()
	{
		OnClose?.Invoke();
		Application.RequestStop(this);
	}
}
