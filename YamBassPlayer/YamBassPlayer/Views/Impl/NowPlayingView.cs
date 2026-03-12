using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class NowPlayingView : Window, INowPlayingView
{
	private readonly Label _artistTitleLabel;
	private readonly Label _albumLabel;
	private readonly SpectrumView _spectrum;
	private readonly Label _listenCountLabel;

	public Action? OnClose;

	public NowPlayingView() : base("Сейчас играет")
	{
		X = 0;
		Y = 0;
		Width = Dim.Fill();
		Height = Dim.Fill();

		var sepTop = new Label
		{
			X = 1,
			Y = 0,
			Width = Dim.Fill() - 2,
			Height = 1,
			Text = new string('═', 300),
			AutoSize = false
		};

		_artistTitleLabel = new Label
		{
			X = 1,
			Y = 1,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Centered,
			AutoSize = false,
			Text = "— — —"
		};

		_albumLabel = new Label
		{
			X = 1,
			Y = 2,
			Width = Dim.Fill() - 2,
			Height = 1,
			TextAlignment = TextAlignment.Centered,
			AutoSize = false,
			Text = ""
		};

		var sepBottom = new Label
		{
			X = 1,
			Y = 3,
			Width = Dim.Fill() - 2,
			Height = 1,
			Text = new string('─', 300),
			AutoSize = false
		};

		_listenCountLabel = new Label
		{
			X = 1,
			Y = Pos.AnchorEnd(6),
			Width = Dim.Fill() - 18,
			Height = 1,
			Text = "Прослушиваний: —"
		};

		var closeButton = new Button
		{
			X = Pos.AnchorEnd(15),
			Y = Pos.AnchorEnd(6),
			Text = "Закрыть [ESC]"
		};
		closeButton.Clicked += () => Close();

		_spectrum = new SpectrumView(bars: 300)
		{
			X = 0,
			Y = 4,
			Width = Dim.Fill(),
			Height = Dim.Fill(6),
			BarWidth = 4,
			BarGap = 1
		};

		Add(sepTop, _artistTitleLabel, _albumLabel, sepBottom,
			_spectrum, _listenCountLabel, closeButton);

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
			_albumLabel.Text = string.IsNullOrWhiteSpace(track.Album) ? "" : $"[ {track.Album} ]";
		});
	}

	public void SetFftData(float[] fft)
	{
		_spectrum.SetFftData(fft);
	}

	public void SetListenCount(int count)
	{
		Application.MainLoop.Invoke(() =>
		{
			_listenCountLabel.Text = $"Прослушиваний: {count}";
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
