using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Spectrum;

namespace YamBassPlayer.Views.Impl;

public sealed class NowPlayingView : Window
{
	private readonly Label _artistTitleLabel;
	private readonly Label _albumLabel;
	private readonly SpectrumView _spectrum;
	private readonly Button _modeButton;
	private readonly Button _freqButton;
	private int _freqPresetIndex = 4;
	private static readonly int[] FreqPresets = [4000, 8000, 12000, 16000, 22050];

	public SpectrumDataType SpectrumDataType => _spectrum.RequiredDataType;

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

		_spectrum = new SpectrumView(bars: 300)
		{
			X = 0,
			Y = 4,
			Width = Dim.Fill(),
			Height = Dim.Fill(6)
		};
		_spectrum.AddRenderer(new BarsRenderer(300) { BarWidth = 4, BarGap = 1 });
		_spectrum.AddRenderer(new OscilloscopeRenderer());
		_spectrum.AddRenderer(new PolarWaveformRenderer());
		_spectrum.AddRenderer(new LissajousScopeRenderer());
		_spectrum.AddRenderer(new WaterfallRenderer());
		_spectrum.AddRenderer(new RingsRenderer());
		_spectrum.AddRenderer(new Tunnel3DRenderer());
		_spectrum.AddRenderer(new StereoPanScopeRenderer());

		var closeButton = new Button
		{
			X = Pos.AnchorEnd(15),
			Y = Pos.AnchorEnd(6),
			Text = "Закрыть [ESC]"
		};
		closeButton.Clicked += () => Close();

		_modeButton = new Button
		{
			X = 0,
			Y = Pos.AnchorEnd(6),
			Text = _spectrum.ModeDisplayName
		};
		_modeButton.Clicked += ToggleMode;

		_freqButton = new Button
		{
			X = Pos.Right(_modeButton) + 1,
			Y = Pos.AnchorEnd(6),
			Text = "▲ 22k"
		};
		_freqButton.Clicked += CycleFreq;

		Add(sepTop, _artistTitleLabel, _albumLabel, sepBottom,
			_spectrum, _modeButton, _freqButton, closeButton);

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

	public void SetSpectrumData(float[] data)
	{
		_spectrum.SetData(data);
	}

	public void SetListenCount(int count) { }

	private void ToggleMode()
	{
		_spectrum.CycleMode();
		_modeButton.Text = _spectrum.ModeDisplayName;
	}

	private void CycleFreq()
	{
		_freqPresetIndex = (_freqPresetIndex + 1) % FreqPresets.Length;
		int freq = FreqPresets[_freqPresetIndex];
		_spectrum.MaxFrequencyHz = freq;
		_freqButton.Text = freq >= 22050 ? "▲ 22k" : $"▲ {freq / 1000}k";
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
