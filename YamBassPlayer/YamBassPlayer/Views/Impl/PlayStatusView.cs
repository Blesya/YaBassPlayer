using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

public sealed class PlayStatusView : View, IPlayStatusView
{
	private readonly Label _statusLabel;
	private readonly FrameView _panel;
	private readonly Button _playButton;
	private readonly Button _stopButton;
	private readonly ProgressBar _progressBar;
	private readonly Button _prevButton;
	private readonly Button _nextButton;

	public event Action? OnPlayClicked;
	public event Action? OnStopClicked;
	public event Action? OnPrevClicked;
	public event Action? OnNextClicked;
	public event Action<int>? OnSeekRequested;

	public PlayStatusView()
	{
	    Width = Dim.Fill();
	    Height = 5;

	    _panel = new FrameView("Управление воспроизведением")
	    {
	        X = 0,
	        Y = 0,
	        Width = Dim.Fill(),
	        Height = Dim.Fill()
	    };

	    _playButton = new Button
	    {
	        X = 1,
	        Y = 0,
	        Text = "Play/Pause"
	    };
	    _playButton.Clicked += () => OnPlayClicked?.Invoke();

	    _stopButton = new Button
	    {
	        X = Pos.Right(_playButton) + 1,
	        Y = 0,
	        Text = "Stop"
	    };
	    _stopButton.Clicked += () => OnStopClicked?.Invoke();

	    _prevButton = new Button
	    {
	        X = Pos.Right(_stopButton) + 1,
	        Y = 0,
	        Text = "Prev"
	    };
	    _prevButton.Clicked += () => OnPrevClicked?.Invoke();

	    _nextButton = new Button
	    {
	        X = Pos.Right(_prevButton) + 1,
	        Y = 0,
	        Text = "Next"
	    };
	    _nextButton.Clicked += () => OnNextClicked?.Invoke();

	    _progressBar = new ProgressBar
	    {
	        X = 1,
	        Y = 1,
	        Width = Dim.Fill() - 2,
	        Height = 1,
	        Fraction = 0f
	    };
	    _progressBar.MouseClick += args =>
	    {
	        if (!args.MouseEvent.Flags.HasFlag(MouseFlags.Button1Clicked))
	            return;

	        int x = args.MouseEvent.X;
	        int width = _progressBar.Bounds.Width;

	        if (width <= 0)
	            return;

	        int percent = (int)(x / (double)width * 100.0);

	        if (percent < 0) percent = 0;
	        if (percent > 100) percent = 100;

	        OnSeekRequested?.Invoke(percent);
	    };

	    _statusLabel = new Label
	    {
	        X = 1,
	        Y = 2,
	        Width = Dim.Fill() - 2,
	        Height = 1,
	        Text = "Готов к работе"
	    };

	    _panel.Add(_playButton, _stopButton, _prevButton, _nextButton, _progressBar, _statusLabel);
	    Add(_panel);
	}

	public void SetPlayStatus(string status)
	{
	    Application.MainLoop.Invoke(() =>
	    {
	        _statusLabel.Text = status;
	    });
	}

	public void SetProgress(int percent)
	{
	    Application.MainLoop.Invoke(() =>
	    {
	        _progressBar.Fraction = percent / 100f;
	    });
	}

	public void SetTitle(string title)
	{
	    Application.MainLoop.Invoke(() =>
	    {
	        _panel.Title = title;
	    });
	}
}
