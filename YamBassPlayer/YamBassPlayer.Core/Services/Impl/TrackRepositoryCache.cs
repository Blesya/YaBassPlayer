using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class TrackRepositoryCache : ITrackRepositoryCache
{
	private readonly Dictionary<SourceId, List<TrackKey>> _searchTrackKeys = new();
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
