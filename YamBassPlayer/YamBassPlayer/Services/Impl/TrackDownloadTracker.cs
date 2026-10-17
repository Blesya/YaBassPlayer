using System.Collections.Concurrent;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Хранит состояние загрузок в памяти и рассылает изменения. Отчёты приходят
/// из потоков загрузки, поэтому подписчик обязан сам перейти в поток UI.
/// </summary>
public sealed class TrackDownloadTracker : ITrackDownloadTracker
{
	private readonly ConcurrentDictionary<string, TrackDownloadState> _states = new(StringComparer.Ordinal);

	public event Action<string, TrackDownloadState>? Changed;

	public IReadOnlyList<(string TrackId, TrackDownloadState State)> GetActive()
		=> _states
			.Where(pair => pair.Value.Status is TrackDownloadStatus.Downloading or TrackDownloadStatus.Failed)
			.Select(pair => (pair.Key, pair.Value))
			.ToList();

	public void MarkDownloading(string trackId)
		=> Set(trackId, new TrackDownloadState(TrackDownloadStatus.Downloading, 0, 0, false));

	public void ReportProgress(string trackId, DownloadProgress progress)
		=> Set(trackId, TrackDownloadState.Downloading(progress));

	public void MarkCached(string trackId) => Set(trackId, TrackDownloadState.Cached);

	public void MarkFailed(string trackId) => Set(trackId, TrackDownloadState.Failed);

	private void Set(string trackId, TrackDownloadState state)
	{
		_states[trackId] = state;
		Changed?.Invoke(trackId, state);
	}
}
