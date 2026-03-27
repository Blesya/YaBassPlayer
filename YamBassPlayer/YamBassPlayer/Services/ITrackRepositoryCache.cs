using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackRepositoryCache
{
	IReadOnlyList<string> FavoriteTrackIds { get; }
	IReadOnlyList<string> LocalSearchTrackIds { get; }
	IReadOnlyList<string> YandexSearchTrackIds { get; }
	IReadOnlyList<string> QueueTrackIds { get; }
	IReadOnlyList<Track> OnSameWaveTracks { get; }
	IReadOnlyList<Track> MyWaveTracks { get; }

	void ReplaceFavoriteTrackIds(IEnumerable<string> trackIds);
	void InsertFavoriteTrackId(string trackId);
	void RemoveFavoriteTrackId(string trackId);

	bool TryGetCustomPlaylistIds(string playlistName, out List<string> trackIds);
	void SetCustomPlaylistIds(string playlistName, List<string> trackIds);

	void ReplaceLocalSearchTracks(IEnumerable<Track> tracks);
	void ReplaceYandexSearchTracks(IEnumerable<Track> tracks);
	void ReplaceQueueTrackIds(IEnumerable<string> trackIds);
	void ReplaceOnSameWaveTracks(IEnumerable<Track> tracks);
	void ReplaceMyWaveTracks(IEnumerable<Track> tracks);
	void AppendMyWaveTracks(IEnumerable<Track> tracks);
}
