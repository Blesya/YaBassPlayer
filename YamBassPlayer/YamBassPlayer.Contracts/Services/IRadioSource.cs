using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Необязательная возможность источника: персональное радио («Моя волна»).
/// Ядро само решает, что делать с полученными треками, и не знает,
/// какой источник их отдал.
/// </summary>
public interface IRadioSource
{
	bool IsSessionActive { get; }

	/// <summary>Запускает персональную станцию.</summary>
	Task<bool> StartPersonalRadioAsync(CancellationToken ct = default);

	/// <summary>Запускает станцию с затравкой по треку.</summary>
	Task<bool> StartTrackRadioAsync(TrackKey key, CancellationToken ct = default);

	/// <summary>Получает следующий батч треков активной станции.</summary>
	Task<RadioBatch> FetchNextBatchAsync(CancellationToken ct = default);

	Task SendTrackStartedAsync(TrackKey key, CancellationToken ct = default);
	Task SendTrackFinishedAsync(TrackKey key, double playedSeconds = 0, CancellationToken ct = default);
	Task SendTrackSkippedAsync(TrackKey key, double playedSeconds = 0, CancellationToken ct = default);

	void Reset();
}

/// <summary>Батч радио: ключи для очереди и уже разобранные треки для кэша.</summary>
public sealed record RadioBatch(IReadOnlyList<TrackKey> Keys, IReadOnlyList<Track> Tracks);
