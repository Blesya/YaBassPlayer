using System.Collections.Concurrent;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Единый владелец соответствия «id → Track»: тонкий потокобезопасный кэш
/// поверх <see cref="ITrackInfoProvider"/>. Ошибочные ответы провайдера не кэшируются.
/// </summary>
public sealed class TrackCatalog : ITrackCatalog
{
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ConcurrentDictionary<string, Track> _cache = new();

	public TrackCatalog(ITrackInfoProvider trackInfoProvider)
	{
		_trackInfoProvider = trackInfoProvider;
	}

	public async Task<Track> GetAsync(string trackId)
	{
		if (_cache.TryGetValue(trackId, out Track? cached))
			return cached;

		Track track = await _trackInfoProvider.GetTrackInfoById(trackId);
		_cache[trackId] = track;
		return track;
	}

	public async Task<IReadOnlyList<Track>> GetManyAsync(IEnumerable<string> trackIds)
	{
		var ids = trackIds.ToList();
		if (ids.Count == 0)
			return [];

		var result = new Track?[ids.Count];
		var missing = new List<string>();
		for (int i = 0; i < ids.Count; i++)
		{
			if (_cache.TryGetValue(ids[i], out Track? cached))
				result[i] = cached;
			else if (!missing.Contains(ids[i]))
				missing.Add(ids[i]);
		}

		if (missing.Count > 0)
		{
			var fetched = (await _trackInfoProvider.GetTracksInfoByIds(missing)).ToList();
			var byId = new Dictionary<string, Track>();
			foreach (Track track in fetched)
			{
				byId[track.Id] = track;
				_cache[track.Id] = track;
			}

			for (int i = 0; i < ids.Count; i++)
			{
				if (result[i] is null && byId.TryGetValue(ids[i], out Track? track))
					result[i] = track;
			}
		}

		return result.Where(t => t is not null).Cast<Track>().ToList();
	}

	public void Invalidate(string trackId) => _cache.TryRemove(trackId, out _);

	public void Clear() => _cache.Clear();
}
