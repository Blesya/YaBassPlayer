using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackRepositoryCache
{
	IReadOnlyList<TrackKey> QueueTrackIds { get; }
	IReadOnlyList<Track> MyWaveTracks { get; }

	bool TryGetSearchTrackIds(SourceId source, out IReadOnlyList<TrackKey> trackKeys);
	void ReplaceSearchTracks(SourceId source, IEnumerable<Track> tracks);

	void ReplaceQueueTrackIds(IEnumerable<TrackKey> trackKeys);
	void ReplaceMyWaveTracks(IEnumerable<Track> tracks);
	void AppendMyWaveTracks(IEnumerable<Track> tracks);

	event Action? MyWaveReplaced;
	event Action? MyWaveAppended;
}
