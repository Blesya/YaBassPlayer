using System.Timers;
using Terminal.Gui;
using YamBassPlayer.Services;
using YamBassPlayer.Views;
using Timer = System.Timers.Timer;

namespace YamBassPlayer.Presenters.Impl;

public class PlayStatusPresenter : IPlayStatusPresenter
{
private readonly IPlayStatusView _view;
private readonly IAudioPlayer _audioPlayer;
private readonly Timer _timer;

public event Action? OnPlayClicked;
public event Action? OnStopClicked;
	public event Action? OnPrevClicked;
	public event Action? OnNextClicked;

	public PlayStatusPresenter(IPlayStatusView view, IAudioPlayer audioPlayer)
{
_view = view;
_audioPlayer = audioPlayer;

_view.OnPlayClicked += () => OnPlayClicked?.Invoke();
_view.OnStopClicked += () => OnStopClicked?.Invoke();
_view.OnPrevClicked += () => OnPrevClicked?.Invoke();
_view.OnNextClicked += () => OnNextClicked?.Invoke();
_view.OnSeekRequested += _audioPlayer.SeekToPercent;

_timer = new Timer(1000);
_timer.Elapsed += TimerOnElapsed;
_timer.AutoReset = true;
_timer.Start();
}

private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
{
int percent = _audioPlayer.GetProgressInPercent();

Application.MainLoop?.Invoke(() =>
{
_view.SetProgress(percent);
});
}

public void SetPlayStatus(string status)
{
_view.SetPlayStatus(status);
}

public void SetTitle(string tilte)
{
_view.SetTitle(tilte);
}
}
