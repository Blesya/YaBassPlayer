using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class LargeTrackInfoView : Window, ILargeTrackInfoView
{
	private const string AsciiRamp = " .:-=+*#%@";
	private readonly Label _artistTitleLabel;
	private readonly Label _albumLabel;
	private readonly Label _listenCountLabel;
	private readonly CoverAsciiView _asciiView;
	private readonly Label _statusLabel;

	public Action? OnClose { get; set; }

    private const int CoverWidth = 100;
    private const int CoverHeight = 55;

    public LargeTrackInfoView() : base("Крупное инфо")
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
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "— — —"
		};

		_albumLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 4,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = string.Empty
		};

		_listenCountLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 6,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "Прослушиваний: —"
		};

		_statusLabel = new Label
		{
			X = Pos.Right(_asciiView) + 2,
			Y = 8,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Left,
			AutoSize = false,
			Text = "Загрузка обложки..."
		};

		var closeButton = new Button
		{
			X = Pos.Center() - 8,
			Y = Pos.AnchorEnd(2),
			Text = "Закрыть [ESC]"
		};
		closeButton.Clicked += Close;

		Add(_asciiView, _artistTitleLabel, _albumLabel, _listenCountLabel, _statusLabel, closeButton);

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
			_albumLabel.Text = string.IsNullOrWhiteSpace(track.Album) ? string.Empty : $"[ {track.Album} ]";
			_statusLabel.Text = "Загрузка обложки...";
		});
	}

	public void SetListenCount(int count)
	{
		Application.MainLoop.Invoke(() =>
		{
			_listenCountLabel.Text = $"Прослушиваний: {count}";
		});
	}

	public void SetCover(string? coverPath)
	{
		Application.MainLoop.Invoke(() =>
		{
			if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath))
			{
				_statusLabel.Text = "Обложка недоступна";
				_asciiView.SetPixels(null);
				return;
			}

			try
			{
                _asciiView.SetPixels(CoverAsciiView.RenderAscii(
                    coverPath,
                    CoverWidth,
                    CoverHeight));

                _statusLabel.Text = $"Обложка: {Path.GetFileName(coverPath)}";
            }
			catch
			{
				_statusLabel.Text = "Не удалось отрисовать обложку";
				_asciiView.SetPixels(null);
			}
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
