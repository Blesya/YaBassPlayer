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
	private readonly IYandexFavoriteService _yandexFavoriteService;
	private readonly Timer _timer;
	private string? _currentTrackId;

	public event Action? OnPlayClicked;
	public event Action? OnStopClicked;
	public event Action? OnPrevClicked;
	public event Action? OnNextClicked;

	public PlayStatusPresenter(IPlayStatusView view, IAudioPlayer audioPlayer, ILocalFavoriteService localFavoriteService, IYandexFavoriteService yandexFavoriteService)
	{
		_view = view;
		_audioPlayer = audioPlayer;
		_localFavoriteService = localFavoriteService;
		_yandexFavoriteService = yandexFavoriteService;

		_view.OnPlayClicked += () => OnPlayClicked?.Invoke();
		_view.OnStopClicked += () => OnStopClicked?.Invoke();
		_view.OnPrevClicked += () => OnPrevClicked?.Invoke();
		_view.OnNextClicked += () => OnNextClicked?.Invoke();
		_view.OnSeekRequested += _audioPlayer.SeekToPercent;
		_view.OnFavoriteToggleClicked += OnFavoriteToggleClickedHandler;
		_view.OnYandexFavoriteToggleClicked += OnYandexFavoriteToggleClickedHandler;

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
		UpdateYandexFavoriteState();
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

	private async void OnYandexFavoriteToggleClickedHandler()
	{
		if (string.IsNullOrEmpty(_currentTrackId))
			return;

		try
		{
			if (_yandexFavoriteService.IsTrackFavorite(_currentTrackId))
			{
				await _yandexFavoriteService.RemoveFromFavorites(_currentTrackId);
			}
			else
			{
				await _yandexFavoriteService.AddToFavorites(_currentTrackId);
			}

			UpdateYandexFavoriteState();
		}
		catch (Exception)
		{
			_view.SetPlayStatus("Ошибка при обновлении лайка Яндекс.Музыки");
		}
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

	private void UpdateYandexFavoriteState()
	{
		if (string.IsNullOrEmpty(_currentTrackId))
		{
			_view.SetYandexFavoriteState(false);
			return;
		}

		bool isFavorite = _yandexFavoriteService.IsTrackFavorite(_currentTrackId);
		_view.SetYandexFavoriteState(isFavorite);
	}
}
