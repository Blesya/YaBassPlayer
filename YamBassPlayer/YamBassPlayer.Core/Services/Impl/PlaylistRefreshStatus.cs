using System.Collections.Concurrent;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Потокобезопасное состояние обновлений источников: результат последнего прохода
/// по каждому источнику. Живёт в памяти — после перезапуска ветка снова считается
/// свежей, пока обновление не провалится.
/// </summary>
public sealed class PlaylistRefreshStatus : IPlaylistRefreshStatus
{
	private readonly ConcurrentDictionary<SourceId, Entry> _entries = new();

	public void ReportSuccess(SourceId source, DateTime utc)
		=> _entries[source] = new Entry(false, utc, null);

	public void ReportFailure(SourceId source, string error, DateTime utc)
		=> _entries[source] = new Entry(true, utc, error);

	public bool IsStale(SourceId source)
		=> _entries.TryGetValue(source, out Entry entry) && entry.Stale;

	public DateTime? GetLastSuccess(SourceId source)
		=> _entries.TryGetValue(source, out Entry entry) ? entry.LastSuccess : null;

	private readonly record struct Entry(bool Stale, DateTime LastSuccess, string? Error);
}
