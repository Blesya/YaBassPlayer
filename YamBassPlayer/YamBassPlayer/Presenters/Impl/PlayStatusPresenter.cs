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
    private readonly ILocalFavoriteService _localFavoriteService;
    private readonly Timer _timer;
    private string? _currentTrackId;

    public event Action? OnPlayClicked;
    public event Action? OnStopClicked;
    public event Action? OnPrevClicked;
    public event Action? OnNextClicked;

    public PlayStatusPresenter(IPlayStatusView view, IAudioPlayer audioPlayer, ILocalFavoriteService localFavoriteService)
    {
        _view = view;
        _audioPlayer = audioPlayer;
        _localFavoriteService = localFavoriteService;

        _view.OnPlayClicked += () => OnPlayClicked?.Invoke();
        _view.OnStopClicked += () => OnStopClicked?.Invoke();
        _view.OnPrevClicked += () => OnPrevClicked?.Invoke();
        _view.OnNextClicked += () => OnNextClicked?.Invoke();
        _view.OnSeekRequested += _audioPlayer.SeekToPercent;
        _view.OnFavoriteToggleClicked += OnFavoriteToggleClickedHandler;

        _timer = new Timer(1000);
        _timer.Elapsed += TimerOnElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        int percent = _audioPlayer.GetProgressInPercent();
        TimeSpan current = _audioPlayer.GetCurrentPosition();
        TimeSpan duration = _audioPlayer.GetDuration();

        Application.MainLoop?.Invoke(() =>
        {
            _view.SetProgress(percent);
            _view.SetTime(current, duration);
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

    public void SetCurrentTrackId(string? trackId)
    {
        _currentTrackId = trackId;
        UpdateFavoriteState();
    }

    private async void OnFavoriteToggleClickedHandler()
    {
        if (string.IsNullOrEmpty(_currentTrackId))
            return;

        if (_localFavoriteService.IsTrackFavorite(_currentTrackId))
        {
            await _localFavoriteService.RemoveFromFavorites(_currentTrackId);
        }
        else
        {
            await _localFavoriteService.AddToFavorites(_currentTrackId);
        }

        UpdateFavoriteState();
    }

    private void UpdateFavoriteState()
    {
        if (string.IsNullOrEmpty(_currentTrackId))
        {
            _view.SetFavoriteState(false);
            return;
        }

        bool isFavorite = _localFavoriteService.IsTrackFavorite(_currentTrackId);
        _view.SetFavoriteState(isFavorite);
    }
}
