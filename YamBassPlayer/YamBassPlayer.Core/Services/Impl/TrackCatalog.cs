using System.Collections.Concurrent;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Единый владелец соответствия «ключ → Track»: тонкий потокобезопасный кэш
/// поверх <see cref="ITrackInfoProvider"/>. Ошибочные ответы провайдера не кэшируются.
/// </summary>
public sealed class TrackCatalog : ITrackCatalog
{
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ConcurrentDictionary<TrackKey, Track> _cache = new();

	public TrackCatalog(ITrackInfoProvider trackInfoProvider)
	{
		_trackInfoProvider = trackInfoProvider;
	}

	public async Task<Track> GetAsync(TrackKey key)
	{
		if (_cache.TryGetValue(key, out Track? cached))
			return cached;

		Track track = await _trackInfoProvider.GetTrackInfoById(key);
		_cache[key] = track;
		return track;
	}

	public async Task<IReadOnlyList<Track>> GetManyAsync(IEnumerable<TrackKey> keys)
	{
		var requested = keys.ToList();
		if (requested.Count == 0)
			return [];

		var result = new Track?[requested.Count];
		var missing = new List<TrackKey>();
		for (int i = 0; i < requested.Count; i++)
		{
			if (_cache.TryGetValue(requested[i], out Track? cached))
				result[i] = cached;
			else if (!missing.Contains(requested[i]))
				missing.Add(requested[i]);
		}

		if (missing.Count > 0)
		{
			var fetched = (await _trackInfoProvider.GetTracksInfoByIds(missing)).ToList();
			var byKey = new Dictionary<TrackKey, Track>();
			foreach (Track track in fetched)
			{
				byKey[track.Key] = track;
				_cache[track.Key] = track;
			}

			for (int i = 0; i < requested.Count; i++)
			{
				if (result[i] is null && byKey.TryGetValue(requested[i], out Track? track))
					result[i] = track;
			}
		}

		return result.Where(t => t is not null).Cast<Track>().ToList();
	}

	public void Invalidate(TrackKey key) => _cache.TryRemove(key, out _);

	public void Clear() => _cache.Clear();
}
