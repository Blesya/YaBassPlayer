using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class TrackRepositoryCache : ITrackRepositoryCache
{
	private readonly Dictionary<SourceId, List<TrackKey>> _searchTrackKeys = new();
	private readonly Dictionary<(SourceId Source, string CacheKey), List<TrackKey>> _playlistTrackKeys = new();
	private readonly object _playlistLock = new();
	private readonly List<TrackKey> _queueTrackKeys = [];
	private readonly List<Track> _myWaveTracks = [];

	public IReadOnlyList<TrackKey> QueueTrackIds => _queueTrackKeys;
	public IReadOnlyList<Track> MyWaveTracks => _myWaveTracks;

	public bool TryGetSearchTrackIds(SourceId source, out IReadOnlyList<TrackKey> trackKeys)
	{
		if (_searchTrackKeys.TryGetValue(source, out List<TrackKey>? keys))
		{
			trackKeys = keys;
			return true;
		}

		trackKeys = [];
		return false;
	}

	public void ReplaceSearchTracks(SourceId source, IEnumerable<Track> tracks)
	{
		if (!_searchTrackKeys.TryGetValue(source, out List<TrackKey>? keys))
		{
			keys = [];
			_searchTrackKeys[source] = keys;
		}

		keys.Clear();
		keys.AddRange(tracks.Select(t => t.Key));
	}

	/// <summary>
	/// Отдаёт копию: список могли пересобрать в другом потоке, пока вызывающая сторона
	/// его перечисляет (пользователь листает дерево, а фоновое обновление пишет состав).
	/// </summary>
	public bool TryGetPlaylistTrackIds(SourceId source, string cacheKey, out IReadOnlyList<TrackKey> trackKeys)
	{
		lock (_playlistLock)
		{
			if (_playlistTrackKeys.TryGetValue((source, cacheKey), out List<TrackKey>? keys))
			{
				trackKeys = keys.ToList();
				return true;
			}
		}

		trackKeys = [];
		return false;
	}

	public void SetPlaylistTrackIds(SourceId source, string cacheKey, IEnumerable<TrackKey> trackKeys)
	{
		lock (_playlistLock)
		{
			if (!_playlistTrackKeys.TryGetValue((source, cacheKey), out List<TrackKey>? keys))
			{
				keys = [];
				_playlistTrackKeys[(source, cacheKey)] = keys;
			}

			keys.Clear();
			keys.AddRange(trackKeys);
		}
	}

	public void ClearPlaylistTrackIds(SourceId source)
	{
		lock (_playlistLock)
		{
			List<(SourceId Source, string CacheKey)> keys = _playlistTrackKeys.Keys
				.Where(key => key.Source == source)
				.ToList();

			foreach ((SourceId Source, string CacheKey) key in keys)
				_playlistTrackKeys.Remove(key);
		}
	}

	public void ReplaceQueueTrackIds(IEnumerable<TrackKey> trackKeys)
	{
		_queueTrackKeys.Clear();
		_queueTrackKeys.AddRange(trackKeys);
	}

	public event Action? MyWaveReplaced;
	public event Action? MyWaveAppended;

	public void ReplaceMyWaveTracks(IEnumerable<Track> tracks)
	{
		_myWaveTracks.Clear();
		_myWaveTracks.AddRange(tracks);
		MyWaveReplaced?.Invoke();
	}

	public void AppendMyWaveTracks(IEnumerable<Track> tracks)
	{
		_myWaveTracks.AddRange(tracks);
		MyWaveAppended?.Invoke();
	}
}
