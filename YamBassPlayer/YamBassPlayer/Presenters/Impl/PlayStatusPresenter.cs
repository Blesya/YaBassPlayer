using System.Timers;
using Microsoft.Data.Sqlite;
using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Services;
using YamBassPlayer.Views;
using Timer = System.Timers.Timer;

namespace YamBassPlayer.Presenters.Impl;

public class PlayStatusPresenter : IPlayStatusPresenter
{
	private const string LocalSourceId = "local";
	private const string YandexSourceId = "yandex";
	private const string LocalFavoriteSourceName = "локальное избранное";
	private const string YandexFavoriteSourceName = "Яндекс.Музыка";
	private readonly IPlayStatusView _view;
	private readonly IAudioPlayer _audioPlayer;
	private readonly ITrackFavoriteService _trackFavoriteService;
	private readonly Timer _timer;
	private string? _currentTrackId;
	private string? _currentTrackSourceType;

	public event Action? OnPlayClicked;
	public event Action? OnStopClicked;
	public event Action? OnPrevClicked;
	public event Action? OnNextClicked;
	public event Action? OnQueueClicked;
	public event Action? OnPlaybackModeToggled;

	public PlayStatusPresenter(IPlayStatusView view, IAudioPlayer audioPlayer, ITrackFavoriteService trackFavoriteService)
	{
		_view = view;
		_audioPlayer = audioPlayer;
		_trackFavoriteService = trackFavoriteService;

		_view.OnPlayClicked += () => OnPlayClicked?.Invoke();
		_view.OnStopClicked += () => OnStopClicked?.Invoke();
		_view.OnPrevClicked += () => OnPrevClicked?.Invoke();
		_view.OnNextClicked += () => OnNextClicked?.Invoke();
		_view.OnQueueClicked += () => OnQueueClicked?.Invoke();
		_view.OnPlaybackModeToggled += () => OnPlaybackModeToggled?.Invoke();
		_view.OnSeekRequested += _audioPlayer.SeekToPercent;
		_view.OnLocalFavoriteToggleClicked += OnLocalFavoriteToggleClickedHandler;
		_view.OnYandexFavoriteToggleClicked += OnYandexFavoriteToggleClickedHandler;

		_timer = new Timer(1000);
		_timer.Elapsed += TimerOnElapsed;
		_timer.AutoReset = true;
		_timer.Start();

		UpdateFavoriteStates();
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

	public void SetPlaybackMode(PlaybackMode mode)
	{
		_view.SetPlaybackMode(mode);
	}

	public void SetCurrentTrack(string? trackId, string? sourceType)
	{
		_currentTrackId = trackId;
		_currentTrackSourceType = sourceType;
		UpdateFavoriteStates();
	}

	private async void OnLocalFavoriteToggleClickedHandler()
	{
		await ToggleFavoriteAsync(LocalSourceId, LocalFavoriteSourceName);
	}

	private async void OnYandexFavoriteToggleClickedHandler()
	{
		await ToggleFavoriteAsync(
			YandexSourceId,
			YandexFavoriteSourceName);
	}

	private async Task ToggleFavoriteAsync(string sourceId, string sourceName)
	{
		if (string.IsNullOrWhiteSpace(_currentTrackId)
			|| !_trackFavoriteService.SupportsSource(sourceId)
			|| !IsFavoriteSourceApplicable(sourceId))
		{
			return;
		}

		try
		{
			if (_trackFavoriteService.IsTrackFavorite(sourceId, _currentTrackId))
			{
				await _trackFavoriteService.RemoveFromFavorites(sourceId, _currentTrackId);
			}
			else
			{
				await _trackFavoriteService.AddToFavorites(sourceId, _currentTrackId);
			}

			UpdateFavoriteState(sourceId);
		}
		catch (HttpRequestException)
		{
			_view.SetPlayStatus(GetFavoriteUpdateErrorStatus(sourceName));
		}
		catch (TaskCanceledException)
		{
			_view.SetPlayStatus(GetFavoriteUpdateErrorStatus(sourceName));
		}
		catch (TimeoutException)
		{
			_view.SetPlayStatus(GetFavoriteUpdateErrorStatus(sourceName));
		}
		catch (SqliteException)
		{
			_view.SetPlayStatus(GetFavoriteUpdateErrorStatus(sourceName));
		}
		catch (InvalidOperationException)
		{
			_view.SetPlayStatus(GetFavoriteUpdateErrorStatus(sourceName));
		}
	}

	private void UpdateFavoriteStates()
	{
		UpdateFavoriteState(LocalSourceId);
		UpdateFavoriteState(YandexSourceId);
	}

	private void UpdateFavoriteState(string sourceId)
	{
		switch (sourceId)
		{
			case LocalSourceId:
				UpdateFavoriteState(
					sourceId,
					_view.SetLocalFavoriteVisibility,
					_view.SetLocalFavoriteEnabled,
					_view.SetLocalFavoriteState);
				break;
			case YandexSourceId:
				UpdateFavoriteState(
					sourceId,
					_view.SetYandexFavoriteVisibility,
					_view.SetYandexFavoriteEnabled,
					_view.SetYandexFavoriteState);
				break;
		}
	}

	private void UpdateFavoriteState(
		string sourceId,
		Action<bool> setVisibility,
		Action<bool> setEnabled,
		Action<bool> setFavoriteState)
	{
		bool isSupported = _trackFavoriteService.SupportsSource(sourceId);
		bool isApplicable = IsFavoriteSourceApplicable(sourceId);
		bool hasTrack = !string.IsNullOrWhiteSpace(_currentTrackId);
		string trackId = _currentTrackId ?? string.Empty;
		bool isFavorite = isSupported
			&& isApplicable
			&& hasTrack
			&& _trackFavoriteService.IsTrackFavorite(sourceId, trackId);

		setVisibility(isSupported && isApplicable);
		setEnabled(isSupported && isApplicable && hasTrack);
		setFavoriteState(isFavorite);
	}

	private bool IsFavoriteSourceApplicable(string sourceId)
	{
		return sourceId switch
		{
			LocalSourceId => true,
			YandexSourceId => string.Equals(_currentTrackSourceType, YandexSourceId, StringComparison.OrdinalIgnoreCase),
			_ => false
		};
	}

	private static string GetFavoriteUpdateErrorStatus(string sourceName)
		=> $"Не удалось обновить избранное: {sourceName}";
}
