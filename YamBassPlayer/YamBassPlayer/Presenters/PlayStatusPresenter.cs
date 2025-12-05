using System.Timers;

using Terminal.Gui;

using YamBassPlayer.Views;
using Timer = System.Timers.Timer;

namespace YamBassPlayer.Presenters;

public class PlayStatusPresenter
{
	private readonly PlayStatusView _view;
	private readonly Timer _timer;

	public event Action? OnPlayClicked;
	public event Action? OnStopClicked;
    public event Action? OnPrevClicked;
    public event Action? OnNextClicked;

    public PlayStatusPresenter(PlayStatusView view)
	{
		_view = view;

		_view.OnPlayClicked += () => OnPlayClicked?.Invoke();
		_view.OnStopClicked += () => OnStopClicked?.Invoke();
		_view.OnPrevClicked += () => OnPrevClicked?.Invoke();
		_view.OnNextClicked += () => OnNextClicked?.Invoke();
		_view.OnSeekRequested += AudioPlayer.SeekToPercent;

		_timer = new Timer(1000);
		_timer.Elapsed += TimerOnElapsed;
		_timer.AutoReset = true;
		_timer.Start();
	}

	private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
	{
		int percent = AudioPlayer.GetProgressInPercent();

		Application.MainLoop?.Invoke(() =>
		{
			_view.SetProgress(percent);
		});
	}

	public void SetPlayStatus(string status)
	{
		_view.SetPlayStatus(status);
	}

	public void SetTilte(string tilte)
	{
		_view.SetTitle(tilte);
	}
}