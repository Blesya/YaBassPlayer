using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackInfoProvider
{
	Task<IEnumerable<Track>> GetTracksInfoByIds(IEnumerable<TrackKey> keys);

	/// <summary>
	/// Возвращает только те треки, что уже есть в локальной БД. В источник не ходит:
	/// используется для мгновенного показа списка до сетевой догрузки.
	/// </summary>
	Task<IReadOnlyList<Track>> GetCachedTracksByIds(IEnumerable<TrackKey> keys);

	Task<Track> GetTrackInfoById(TrackKey key);
	Task SaveAsync(Track track);
	Task<bool> IsTrackCached(TrackKey key);
	Task<int> CountCachedTracks(IEnumerable<TrackKey> keys);
	Task<IEnumerable<Track>> SearchTracks(string searchQuery, int maxResults = 50);
	Task<IReadOnlyList<(string artistName, int trackCount)>> GetArtistsWithTrackCountAsync();
	Task<IReadOnlyList<TrackKey>> GetTrackIdsByArtistAsync(string artistName);
}
