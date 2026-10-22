using System.Timers;
using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views;
using Timer = System.Timers.Timer;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Панель управления. Действия над треком (лайк, «не предлагать» и т. п.)
/// приходят от плагинов как <see cref="ITrackAction"/>: ядро только рисует
/// кнопки и передаёт нажатие владельцу действия.
/// </summary>
public class PlayStatusPresenter : IPlayStatusPresenter
{
	private readonly IPlayStatusView _view;
	private readonly IAudioPlayer _audioPlayer;
	private readonly TrackActionRegistry _actions;
	private readonly ITrackCatalog _trackCatalog;
	private readonly Timer _timer;
	private Track? _currentTrack;
	private TrackKey? _currentTrackKey;
	private int _trackVersion;

	public event Action? OnPlayClicked;
	public event Action? OnStopClicked;
	public event Action? OnPrevClicked;
	public event Action? OnNextClicked;
	public event Action? OnQueueClicked;
	public event Action? OnPlaybackModeToggled;
	public event Action? OnRestartClicked;

	public PlayStatusPresenter(
		IPlayStatusView view,
		IAudioPlayer audioPlayer,
		TrackActionRegistry actions,
		ITrackCatalog trackCatalog)
	{
		_view = view;
		_audioPlayer = audioPlayer;
		_actions = actions;
		_trackCatalog = trackCatalog;

		_view.OnPlayClicked += () => OnPlayClicked?.Invoke();
		_view.OnStopClicked += () => OnStopClicked?.Invoke();
		_view.OnPrevClicked += () => OnPrevClicked?.Invoke();
		_view.OnNextClicked += () => OnNextClicked?.Invoke();
		_view.OnQueueClicked += () => OnQueueClicked?.Invoke();
		_view.OnPlaybackModeToggled += () => OnPlaybackModeToggled?.Invoke();
		_view.OnRestartClicked += () => OnRestartClicked?.Invoke();
		_view.OnSeekRequested += _audioPlayer.SeekToPercent;
		_view.OnVolumeChangeRequested += OnVolumeChangeRequested;
		_view.OnTrackActionClicked += id => OnTrackActionClickedAsync(id).Forget();

		_actions.Changed += UpdateActionButtons;

		_timer = new Timer(1000);
		_timer.Elapsed += TimerOnElapsed;
		_timer.AutoReset = true;
		_timer.Start();

		_view.SetVolume(_audioPlayer.VolumePercent);
	}

	/// <summary>
	/// Громкость меняют и полоса в панели, и команда <c>volume</c> (она работает с сервисом
	/// напрямую), поэтому вью обновляется из одного места — здесь.
	/// </summary>
	private void OnVolumeChangeRequested(int percent)
	{
		_audioPlayer.SetVolumePercent(percent);
		_view.SetVolume(_audioPlayer.VolumePercent);
		Logging.LogUserAction($"громкость: {_audioPlayer.VolumePercent}%");
	}

	private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
	{
		int percent = _audioPlayer.GetProgressInPercent();
		TimeSpan current = _audioPlayer.GetCurrentPosition();
		TimeSpan duration = _audioPlayer.GetDuration();

		Application.MainLoop?.Invoke(() =>
		{
			_view.SetPlaying(_audioPlayer.IsPlayed);
			_view.SetProgress(percent);
			_view.SetTime(current, duration);
			_view.SetVolume(_audioPlayer.VolumePercent);
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

	public void SetCurrentTrack(TrackKey? key)
	{
		_currentTrackKey = key;
		int version = ++_trackVersion;

		// Сразу показываем действия по ключу, затем подменяем трек полными
		// данными из каталога — действиям нужны Title/Artist, а не только id.
		_currentTrack = key is { } current
			? new Track(string.Empty, string.Empty, string.Empty, current.TrackId) { SourceId = current.SourceId }
			: null;

		UpdateActionButtons();

		if (key is { } trackKey)
			_ = LoadTrackAsync(trackKey, version);
	}

	private async Task LoadTrackAsync(TrackKey key, int version)
	{
		try
		{
			Track? track = await _trackCatalog.GetAsync(key);
			if (track is null || version != _trackVersion || _currentTrackKey != key)
				return;

			_currentTrack = track;
			UpdateActionButtons();
		}
		catch (Exception ex)
		{
			Serilog.Log.Debug(ex, "Не удалось загрузить трек для действий панели {Key}", key);
		}
	}

	private void UpdateActionButtons()
	{
		IReadOnlyList<TrackActionButton> buttons = _currentTrack is null
			? []
			: _actions.Actions
				.Where(action => action.IsAvailable(_currentTrack))
				.Select(action => new TrackActionButton(action.Id, action.GetTitle(_currentTrack), true))
				.ToList();

		_view.SetTrackActions(buttons);
	}

	private async Task OnTrackActionClickedAsync(string id)
	{
		ITrackAction? action = _actions.Actions.FirstOrDefault(candidate => candidate.Id == id);
		if (action is null || _currentTrack is null || !action.IsAvailable(_currentTrack))
			return;

		try
		{
			await action.ExecuteAsync(_currentTrack);
		}
		catch (Exception ex)
		{
			_view.SetPlayStatus($"Не удалось выполнить действие: {ex.Message}");
		}

		UpdateActionButtons();
	}
}
