using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Services.Impl;

public sealed class ListenTimer(IHistoryService historyService, IEventBus eventBus) : IListenTimer
{
	/// <summary>
	/// Наш порог засчитанного прослушивания. ListenBrainz рекомендует «половину
	/// трека или 4 минуты», но проверяет это только на своей стороне как совет:
	/// отправляем то, что уже засчитали сами, и честно сообщаем <c>duration_played_ms</c>.
	/// </summary>
	private const double ListenThresholdSeconds = 30;

	private CancellationTokenSource? _cts;
	private TimeSpan _remaining = TimeSpan.FromSeconds(ListenThresholdSeconds);
	private DateTime _lastPlayUtc;
	private TrackKey? _trackKey;
	private PlaylistContext _context;

	public void OnTrackStart(TrackKey key, PlaylistContext context)
	{
		Logging.LogBeforeCall();

		ResetInternal();
		_trackKey = key;
		_context = context;
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

		// Отсчёт перезапускается, только если трек ещё активен. После успешного
		// LogListen таймер сбрасывается (_trackKey = null), поэтому Resume после
		// паузы не должен запускать отсчёт с пустым ключом.
		if (_trackKey is null || _remaining <= TimeSpan.Zero)
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
		TrackKey key = _trackKey!.Value;
		var rem = _remaining;
		var context = _context;

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(rem, localCts.Token);
				LogListen(key, context);
				ResetInternal();
			}
			catch (OperationCanceledException)
			{
				// Пауза, смена трека или остановка — отсчёт отменён штатно.
			}
			catch (Exception ex)
			{
				// Задача запущена «в фоне»: исключение нельзя терять, иначе оно
				// всплывёт как UnobservedTaskException на finalizer-потоке.
				Log.Error(ex, "Не удалось записать прослушивание трека {TrackKey}", key);
			}
		}, localCts.Token);
	}

	private void ResetInternal()
	{
		_cts?.Cancel();
		_cts = null;
		_remaining = TimeSpan.FromSeconds(ListenThresholdSeconds);
		_trackKey = null;
	}

	private void LogListen(TrackKey key, PlaylistContext context)
	{
		historyService.LogListen(key, context);

		// Одно событие — один владелец истории. Внешние сервисы (ListenBrainz)
		// подписываются на него, а не на таймер: порог остаётся в одном месте.
		eventBus.Publish(new ListenRecordedEvent(key, ListenThresholdSeconds));
	}
}
