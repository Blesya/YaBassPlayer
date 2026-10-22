using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackRepositoryCache
{
	IReadOnlyList<TrackKey> QueueTrackIds { get; }
	IReadOnlyList<Track> MyWaveTracks { get; }

	bool TryGetSearchTrackIds(SourceId source, out IReadOnlyList<TrackKey> trackKeys);
	void ReplaceSearchTracks(SourceId source, IEnumerable<Track> tracks);

	/// <summary>
	/// Состав обычного плейлиста источника, загруженный в этом запуске.
	/// Ключ — <see cref="Playlist.CacheKey"/>: у Яндекса это «владелец:kind», поэтому
	/// одноимённые плейлисты разных владельцев не перетирают друг друга.
	/// </summary>
	bool TryGetPlaylistTrackIds(string cacheKey, out IReadOnlyList<TrackKey> trackKeys);
	void SetPlaylistTrackIds(string cacheKey, IEnumerable<TrackKey> trackKeys);

	/// <summary>Сбрасывает состав всех плейлистов: вызывается при обновлении дерева из источников.</summary>
	void ClearPlaylistTrackIds();

	void ReplaceQueueTrackIds(IEnumerable<TrackKey> trackKeys);
	void ReplaceMyWaveTracks(IEnumerable<Track> tracks);
	void AppendMyWaveTracks(IEnumerable<Track> tracks);

	event Action? MyWaveReplaced;
	event Action? MyWaveAppended;
}
