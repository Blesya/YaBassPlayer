using System.Collections.Concurrent;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Хранит состояние загрузок в памяти и рассылает изменения. Отчёты приходят
/// из потоков загрузки, поэтому подписчик обязан сам перейти в поток UI.
/// </summary>
public sealed class TrackDownloadTracker : ITrackDownloadTracker
{
	private readonly ConcurrentDictionary<TrackKey, TrackDownloadState> _states = new();

	public event Action<TrackKey, TrackDownloadState>? Changed;

	public IReadOnlyList<(TrackKey Key, TrackDownloadState State)> GetActive()
		=> _states
			.Where(pair => pair.Value.Status is TrackDownloadStatus.Downloading or TrackDownloadStatus.Failed)
			.Select(pair => (pair.Key, pair.Value))
			.ToList();

	public void MarkDownloading(TrackKey key)
		=> Set(key, new TrackDownloadState(TrackDownloadStatus.Downloading, 0, 0, false));

	public void ReportProgress(TrackKey key, DownloadProgress progress)
		=> Set(key, TrackDownloadState.Downloading(progress));

	public void MarkCached(TrackKey key) => Set(key, TrackDownloadState.Cached);

	public void MarkFailed(TrackKey key) => Set(key, TrackDownloadState.Failed);

	private void Set(TrackKey key, TrackDownloadState state)
	{
		_states[key] = state;
		Changed?.Invoke(key, state);
	}
}
