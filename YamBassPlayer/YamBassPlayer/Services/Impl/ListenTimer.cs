using YamBassPlayer.Enums;

namespace YamBassPlayer.Services.Impl;

public sealed class ListenTimer(IHistoryService historyService) : IListenTimer
{
	private CancellationTokenSource? _cts;
	private TimeSpan _remaining = TimeSpan.FromSeconds(30);
	private DateTime _lastPlayUtc;
	private string? _trackId;
	private ListenSource _source;

	public void OnTrackStart(string trackId, ListenSource source)
	{
		Logging.LogBeforeCall();

		ResetInternal();
		_trackId = trackId;
		_source = source;
		StartCountdown();

		Logging.LogAfterCall();
	}

	public void OnPause()
	{
		Logging.LogBeforeCall();

		if (_cts == null)
			return;

		var now = DateTime.UtcNow;
		var delta = now - _lastPlayUtc;
		_remaining -= delta;

		_cts.Cancel();

		Logging.LogAfterCall();
	}

	public void OnResume()
	{
		Logging.LogBeforeCall();

		if (_remaining <= TimeSpan.Zero)
			return;

		StartCountdown();

		Logging.LogAfterCall();
	}

	public void OnTrackStopOrChange()
	{
		Logging.LogBeforeCall();

		ResetInternal();

		Logging.LogAfterCall();
	}

	private void StartCountdown()
	{
		_cts = new CancellationTokenSource();
		_lastPlayUtc = DateTime.UtcNow;

		var localCts = _cts;
		var track = _trackId!;
		var rem = _remaining;
		var source = _source;

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(rem, localCts.Token);
				LogListen(track, source);
				ResetInternal();
			}
			catch (TaskCanceledException)
			{
			}
		}, localCts.Token);
	}

	private void ResetInternal()
	{
		_cts?.Cancel();
		_cts = null;
		_remaining = TimeSpan.FromSeconds(30);
		_trackId = null;
	}

	private void LogListen(string trackId, ListenSource source)
	{
		historyService.LogListen(trackId, source);
	}
}